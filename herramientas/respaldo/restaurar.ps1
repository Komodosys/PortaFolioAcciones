# Restauracion de un respaldo.
#
# **Por omision NO toca la base de datos buena.** Restaura en una base aparte, la de comprobacion, y sirve
# para lo que de verdad hace falta saber: que el respaldo se puede restaurar. Restaurar encima de los datos
# de verdad es una decision, y por eso hay que pedirlo con `-Real`.
#
# El orden, cuando se pide la de verdad, es el de siempre: copia de seguridad, parar, restaurar, arrancar.
# Si algo falla, NO se arranca, para que nadie se quede con una base a medias creyendo que esta bien.
#
# Uso:
#   restaurar.cmd                          restaura el respaldo mas reciente en la base de comprobacion
#   restaurar.cmd -Fichero <ruta>          uno concreto, en la base de comprobacion
#   restaurar.cmd -Real                    el mas reciente, ENCIMA de la base de verdad (pide confirmacion)

param(
    [string] $Fichero = '',
    [string] $BaseDeComprobacion = 'portafolio_comprobacion',
    [switch] $Real
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$raiz = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
Set-Location $raiz

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

# --- Que se restaura, y donde -----------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($Fichero)) {
    $ultimo = @(Get-ChildItem 'respaldos' -Filter 'portafolio-*.dump' | Sort-Object Name -Descending)

    if ($ultimo.Count -eq 0) {
        Write-Host '  No hay ningun respaldo en respaldos\.' -ForegroundColor Red
        exit 1
    }

    $Fichero = $ultimo[0].FullName
    Write-Host "  Respaldo mas reciente: $($ultimo[0].Name)" -ForegroundColor Cyan
}

if (-not (Test-Path $Fichero)) {
    Write-Host "  No existe el fichero $Fichero." -ForegroundColor Red
    exit 1
}

$destino = if ($Real) { $base } else { $BaseDeComprobacion }

Write-Host "  Destino: $destino" -ForegroundColor Cyan

if ($Real) {
    Write-Host ''
    Write-Host '  ATENCION: se va a SUSTITUIR el contenido de la base de datos de verdad.' -ForegroundColor Yellow
    Write-Host '  Se hara antes una copia de seguridad de lo que hay ahora, pero conviene estar seguro.' -ForegroundColor Yellow

    $respuesta = Read-Host '  Escribe RESTAURAR para continuar'

    if ($respuesta -ne 'RESTAURAR') {
        Write-Host '  Cancelado. No se ha tocado nada.' -ForegroundColor DarkGray
        exit 0
    }
}

# --- 1. Copia de lo que hay ahora, si se va a tocar la de verdad -------------------------------
if ($Real) {
    Write-Host ''
    Write-Host '  Copia de seguridad de la base actual ...' -ForegroundColor Cyan

    & powershell -NoProfile -ExecutionPolicy Bypass -File 'herramientas\respaldo\respaldar.ps1' -Retencion 30

    if ($LASTEXITCODE -ne 0) {
        Write-Host '  La copia ha fallado. NO se restaura nada.' -ForegroundColor Red
        exit 1
    }

    Write-Host '  Parando la API ...' -ForegroundColor Cyan
    docker compose stop api | Out-Null
}

# --- 2. La base de destino --------------------------------------------------------------------
# Se crea si no existe. Se DESTRUYE y se vuelve a crear si ya existia: restaurar encima dejaria una mezcla
# de dos estados, que es peor que cualquiera de los dos.
Write-Host '  Preparando la base de destino ...' -ForegroundColor Cyan

$existe = cmd /c "docker compose exec -T postgres psql -U $usuario -d postgres -tAc ""SELECT 1 FROM pg_database WHERE datname='$destino'"" 2>&1"

if ($existe -match '1') {
    $null = cmd /c "docker compose exec -T postgres psql -U $usuario -d postgres -c ""DROP DATABASE IF EXISTS $destino WITH (FORCE)"" 2>&1"
}

$null = cmd /c "docker compose exec -T postgres psql -U $usuario -d postgres -c ""CREATE DATABASE $destino"" 2>&1"

if ($LASTEXITCODE -ne 0) {
    Write-Host '  No se pudo crear la base de destino.' -ForegroundColor Red

    if ($Real) { docker compose up -d api | Out-Null }

    exit 1
}

# --- 3. Restaurar -----------------------------------------------------------------------------
Write-Host '  Restaurando ...' -ForegroundColor Cyan

# El fichero se copia AL CONTENEDOR: `pg_restore` corre alli, y desde fuera no veria la ruta del host.
$dentro = '/tmp/restauracion.dump'
docker cp $Fichero "portafolio-postgres:$dentro" | Out-Null

$null = cmd /c "docker compose exec -T postgres pg_restore -U $usuario -d $destino --no-owner --no-acl $dentro 2>&1"
$resultado = $LASTEXITCODE

docker compose exec -T postgres rm -f $dentro | Out-Null

if ($resultado -ne 0) {
    Write-Host '  LA RESTAURACION HA FALLADO.' -ForegroundColor Red

    if ($Real) {
        Write-Host '  La API sigue parada a proposito. Revisa el error antes de arrancarla.' -ForegroundColor Red
    }

    exit 1
}

# --- 4. Comprobar que lo restaurado tiene algo -------------------------------------------------
# Que `pg_restore` diga que fue bien no basta: lo que hay que saber es que los datos estan.
#
# La consulta va en un FICHERO y no en la linea de comandos, y no es un capricho: al pasar por cmd, las
# comillas dobles de los nombres de tabla se pierden y PostgreSQL los lee en minusculas —«userprofiles»—, y
# entonces la comprobacion falla por el entrecomillado y no porque el respaldo este mal. Es un error que ya
# ha aparecido varias veces en este proyecto, y siempre con la misma forma.
if (-not (Test-Path '.tmp')) {
    New-Item -ItemType Directory -Path '.tmp' | Out-Null
}

$ficheroComprobacion = Join-Path $raiz '.tmp\comprobar-restauracion.sql'

@'
SELECT
    'Users=' || (SELECT count(*) FROM "UserProfiles" ) ||
    ' Portfolio=' || (SELECT count(*) FROM "Portfolios" ) ||
    ' Instruments=' || (SELECT count(*) FROM "Instruments" ) ||
    ' Positions=' || (SELECT count(*) FROM "Positions" ) ||
    ' Lots=' || (SELECT count(*) FROM "Lots" )
'@ | Set-Content -LiteralPath $ficheroComprobacion -Encoding ascii

docker cp $ficheroComprobacion 'portafolio-postgres:/tmp/comprobar-restauracion.sql' | Out-Null

$cuentas = cmd /c "docker compose exec -T postgres psql -U $usuario -d $destino -tAf /tmp/comprobar-restauracion.sql 2>&1"
$comprobacionOk = ($LASTEXITCODE -eq 0)

docker compose exec -T postgres rm -f /tmp/comprobar-restauracion.sql | Out-Null
Remove-Item $ficheroComprobacion -Force -ErrorAction SilentlyContinue

if (-not $comprobacionOk) {
    Write-Host '  La restauracion termino, pero NO se pudo comprobar lo que hay dentro:' -ForegroundColor Red
    Write-Host "  $cuentas" -ForegroundColor Red

    if ($Real) {
        Write-Host '  La API sigue parada a proposito. Revisalo antes de arrancarla.' -ForegroundColor Red
    }

    exit 1
}

Write-Host "  Contenido restaurado: $cuentas" -ForegroundColor Green

if ($cuentas -match 'Users=0' -and $cuentas -match 'Portfolio=0') {
    Write-Host '  Aviso: lo restaurado esta vacio. El respaldo podria ser de una base sin datos.' -ForegroundColor Yellow
}

if ($Real) {
    Write-Host '  Arrancando la API ...' -ForegroundColor Cyan
    docker compose up -d | Out-Null
}

Write-Host ''
Write-Host "  Restauracion completada en la base '$destino'." -ForegroundColor Green

if (-not $Real) {
    Write-Host '  La base de verdad NO se ha tocado. Para borrar la de comprobacion:' -ForegroundColor DarkGray
    Write-Host "    docker compose exec -T postgres psql -U $usuario -d postgres -c ""DROP DATABASE $destino WITH (FORCE)""" -ForegroundColor DarkGray
}

exit 0
