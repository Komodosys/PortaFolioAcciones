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
    [string] $Carpeta = 'respaldos'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
Set-Location $raiz

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
exit 0
