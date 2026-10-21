# Respaldo de la base de datos, con retencion.
#
# Lo que hace, en orden:
#
#   1. Vuelca la base en formato COMPRIMIDO de PostgreSQL (no en SQL de texto).
#   2. COMPRUEBA que el volcado se puede leer. Un respaldo que nunca se ha comprobado no es un respaldo:
#      es un fichero del que se supone que sirve.
#   3. Borra los mas antiguos, dejando los ultimos N.
#
# Dos decisiones que no son de estilo:
#
#   * **El volcado se hace CON `-f` dentro del contenedor y se saca con `docker cp`**, en vez de
#     redirigir la salida. Redirigir un flujo binario desde PowerShell lo corrompe en silencio: el fichero
#     queda con el tamaño correcto y no se puede restaurar, y eso se descubre el dia que hace falta.
#   * **Se usa el formato comprimido (`--format=custom`)**, no SQL de texto. Ocupa mucho menos, y sobre
#     todo permite comprobarlo con `pg_restore --list` sin restaurarlo entero.

param(
    [int] $Retencion = 14,
    [string] $Carpeta = 'respaldos',

    # El remoto de rclone, **cifrado**: el fichero sale de aqui ya cifrado.
    [string] $Remoto = 'portafolio-cifrado:',

    # Cuantos dias se guardan en el destino. Mas que en local a proposito: si algo se rompe, se descubre
    # dias despues, y para entonces el respaldo bueno puede haberse ido de la rotacion local.
    [int] $DiasEnLaNube = 30,

    # Saltarse la copia fuera del equipo. Existe para poder respaldar sin red, diciendolo.
    [switch] $SinNube
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
Set-Location $raiz

# --- rclone, lanzado por cmd /c ---------------------------------------------------------------
# No es un capricho. En PowerShell, la salida de error de un programa externo se convierte en un error de
# PowerShell y, con $ErrorActionPreference = 'Stop', **corta el script**. rclone escribe avisos normales por
# stderr —el del client_id compartido, sin ir más lejos—, así que llamarlo directo mataba el respaldo *después*
# de haberlo hecho bien, y en un sitio donde el fichero local ya estaba creado y comprobado.
#
# Es exactamente la misma razón por la que `pg_restore` se lanza con `cmd /c` más arriba. Estaba escrito en este
# mismo fichero y volví a tropezar con ello.
#
# Devuelve el código y la salida juntos: con `$LASTEXITCODE` suelto, la siguiente llamada lo pisa.
function Rclone {
    param([string[]] $Argumentos)

    # `--log-level ERROR` para que los avisos no se mezclen con lo que se lee. rclone avisa por stderr de que
    # el client_id compartido se retira, y ese texto acababa dentro del JSON que luego se intentaba leer.
    $linea = "`"$rcloneExe`" " + ($Argumentos -join ' ') + ' --log-level ERROR'

    # `2>&1` va DENTRO de lo que se le pasa a cmd: es lo que mezcla el stderr de rclone con su salida.
    $salida = cmd /c "$linea 2>&1"

    return @{ Codigo = $LASTEXITCODE; Salida = @($salida) }
}

# --- Las credenciales, del .env ---------------------------------------------------------------
if (-not (Test-Path '.env')) {
    Write-Host '  No existe el fichero .env.' -ForegroundColor Red
    exit 1
}

$variables = @{}

foreach ($linea in Get-Content '.env') {
    if ($linea -match '^\s*#' -or $linea -notmatch '=') { continue }

    $partes = $linea -split '=', 2
    $variables[$partes[0].Trim()] = $partes[1].Trim()
}

$usuario = $variables['POSTGRES_USER']
$base = $variables['POSTGRES_DB']

if (-not $usuario -or -not $base) {
    Write-Host '  Faltan POSTGRES_USER o POSTGRES_DB en el .env.' -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $Carpeta)) {
    New-Item -ItemType Directory -Path $Carpeta | Out-Null
}

# --- 1. El volcado ----------------------------------------------------------------------------
$marca = Get-Date -Format 'yyyy-MM-dd-HHmmss'
$nombre = "portafolio-$marca.dump"
$dentro = "/tmp/$nombre"
$destino = Join-Path $Carpeta $nombre

Write-Host "  Volcando $base ..." -ForegroundColor Cyan

docker compose exec -T postgres pg_dump -U $usuario -d $base --format=custom --no-owner --no-acl -f $dentro

if ($LASTEXITCODE -ne 0) {
    Write-Host '  El volcado HA FALLADO. No se ha creado ningun respaldo.' -ForegroundColor Red
    exit 1
}

docker cp "portafolio-postgres:$dentro" $destino | Out-Null

if (-not (Test-Path $destino) -or (Get-Item $destino).Length -eq 0) {
    Write-Host '  El fichero de respaldo esta vacio.' -ForegroundColor Red
    exit 1
}

$tamano = [math]::Round((Get-Item $destino).Length / 1KB, 0)

# --- 2. La comprobacion -----------------------------------------------------------------------
# Se lee el indice del volcado ANTES de borrar el temporal del contenedor. Es barato y detecta lo que de
# verdad falla: un fichero truncado o corrupto. No sustituye a restaurar de verdad —eso se hace aparte, y
# esta documentado—, pero evita guardar como bueno un fichero que ya se sabe que no sirve.
#
# Se lanza con `cmd /c` y no directamente porque en PowerShell la salida de error de un programa externo
# se convierte en un error de PowerShell, y con `$ErrorActionPreference = 'Stop'` eso corta el script por
# un fallo que aqui es NORMAL y se esta comprobando a proposito.
$null = cmd /c "docker compose exec -T postgres pg_restore --list $dentro 2>&1"
$legible = ($LASTEXITCODE -eq 0)

docker compose exec -T postgres rm -f $dentro | Out-Null

if (-not $legible) {
    Write-Host '  El volcado NO se puede leer. Se deja el fichero para poder mirarlo.' -ForegroundColor Red
    Write-Host "  $destino" -ForegroundColor Red
    exit 1
}

Write-Host "  Respaldo comprobado: $destino ($tamano KB)" -ForegroundColor Green

# --- 3. La retencion --------------------------------------------------------------------------
# Se conservan los N mas recientes POR FECHA DE NOMBRE, no por fecha del fichero: copiar la carpeta a
# otro sitio cambia las fechas del sistema de ficheros, y entonces la retencion borraria los que no toca.
#
# El `@(...)` no es adorno: con un solo respaldo, `Get-ChildItem` devuelve UN objeto y no una lista, y
# `.Count` no existe — con `StrictMode` eso corta el script justo despues de haber hecho el respaldo.
$todos = @(Get-ChildItem $Carpeta -Filter 'portafolio-*.dump' | Sort-Object Name -Descending)

if ($todos.Count -gt $Retencion) {
    $sobran = $todos[$Retencion..($todos.Count - 1)]

    foreach ($viejo in $sobran) {
        Remove-Item $viejo.FullName -Force
        Write-Host "  Borrado el respaldo antiguo: $($viejo.Name)" -ForegroundColor DarkGray
    }
}

Write-Host "  Quedan $([math]::Min($todos.Count, $Retencion)) respaldos (retencion: $Retencion)." -ForegroundColor Cyan

# --- 4. La copia fuera del equipo -------------------------------------------------------------
# El respaldo de arriba vive en el MISMO disco que la base de datos. Eso protege de un error —una migracion,
# un borrado equivocado— pero no de que el disco muera. Para eso hay que sacarlo de la maquina.
#
# Se sube **cifrado**: el remoto de rclone es de tipo `crypt`, asi que el fichero sale de aqui ya cifrado y
# en Drive queda ilegible. La contrapartida es que la contrasena es imprescindible para restaurar, y por eso
# no vive en este repositorio.
#
# **Si la nube falla, el script falla.** El respaldo local ya esta hecho y comprobado, pero una copia fuera
# que lleva un mes sin subir es peor que no tenerla: se daria por hecha. Que la tarea de Windows marque error
# es lo que hace que se vea.
if ($SinNube) {
    Write-Host '  Copia fuera del equipo: OMITIDA (se pidio con -SinNube).' -ForegroundColor Yellow
    exit 0
}

$rclone = Get-Command rclone -ErrorAction SilentlyContinue
$rcloneExe = if ($rclone) { $rclone.Source } else { $null }

# Una tarea programada no hereda el PATH de la consola, asi que se busca tambien donde winget lo deja.
if (-not $rcloneExe) {
    $rcloneExe = @(
        Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter 'rclone.exe' -ErrorAction SilentlyContinue
        Get-ChildItem "$env:ProgramFiles\rclone" -Filter 'rclone.exe' -ErrorAction SilentlyContinue
    ) | Select-Object -First 1 -ExpandProperty FullName
}

if (-not $rcloneExe) {
    Write-Host '  Copia fuera del equipo: NO se puede: falta rclone.' -ForegroundColor Red
    Write-Host '  Instalalo con: winget install Rclone.Rclone' -ForegroundColor Red
    exit 1
}

# Si el remoto no esta configurado todavia, se dice claro y se falla: no se puede dar por hecho.
#
# El `-join` no es cosmetico: `listremotes` devuelve una LISTA, y comparar una lista con `-notmatch` no da un
# booleano —devuelve los elementos que no casan—, que en un `if` siempre es verdadero. Con eso, el script decia
# «el remoto no esta configurado» teniendolo configurado delante.
$remotos = (Rclone 'listremotes').Salida -join "`n"
if ($remotos -notmatch [regex]::Escape($Remoto)) {
    Write-Host "  Copia fuera del equipo: NO se puede: el remoto '$Remoto' no esta configurado." -ForegroundColor Red
    Write-Host '  Configuralo una vez con: rclone config' -ForegroundColor Red
    exit 1
}

$nombreDelFichero = Split-Path $destino -Leaf
Write-Host "  Subiendo a $Remoto (cifrado) ..." -ForegroundColor Cyan

$subida = Rclone @('copy', "`"$destino`"", "`"$Remoto`"")
if ($subida.Codigo -ne 0) {
    Write-Host '  La copia fuera del equipo HA FALLADO. El respaldo local esta hecho.' -ForegroundColor Red
    exit 1
}

# Y se comprueba que llego, que es lo que convierte una copia en un respaldo.
#
# Se usa `rclone check` y no se lee el JSON de `lsjson`: interpretar una salida pensada para programas era
# fragility añadida, y fallaba por el formato. `check` compara el fichero de verdad —tamaño y, si el remoto lo
# admite, huella— y contesta con el código de salida, que es lo que hace falta.
#
# `--one-way` porque solo importa que esté LO QUE SE ACABA DE SUBIR: en el destino hay más días, y sin esto
# `check` se quejaría de que aquí faltan ficheros que allí sí están.
$comprobacion = Rclone @(
    'check', "`"$Carpeta`"", "`"$Remoto`"",
    '--include', "`"$nombreDelFichero`"",
    '--one-way'
)

if ($comprobacion.Codigo -ne 0) {
    Write-Host '  El fichero NO se ha podido comprobar en el remoto. Se da por fallida la copia.' -ForegroundColor Red
    $comprobacion.Salida | Select-Object -First 5 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    exit 1
}

$tamanoRemoto = (Get-Item $destino).Length
Write-Host "  Copia fuera del equipo comprobada: $Remoto$nombreDelFichero ($tamanoRemoto bytes)." -ForegroundColor Green

# Retencion tambien en el destino: lo local se queda en $Retencion, lo de la nube en $DiasEnLaNube.
if ($DiasEnLaNube -gt 0) {
    $limpieza = Rclone @('delete', "`"$Remoto`"", '--min-age', "`"$($DiasEnLaNube)d`"")

    if ($limpieza.Codigo -ne 0) {
        Write-Host '  No se pudo limpiar la nube. No es grave: se deja dicho.' -ForegroundColor Yellow
    }
}

exit 0
