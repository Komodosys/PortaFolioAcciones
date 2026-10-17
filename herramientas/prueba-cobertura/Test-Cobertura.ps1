<#
.SYNOPSIS
    Prueba de cobertura de instrumentos reales contra la fuente de precios candidata.

.DESCRIPTION
    Lee un CSV de instrumentos y comprueba, uno a uno, si la fuente los cubre y con qué calidad.
    No requiere ninguna clave de API (usa el endpoint público de gráficos de Yahoo Finance).

    Para cada instrumento informa de:
      - si el símbolo existe en la fuente y cómo se resolvió (directo, por ISIN o por nombre);
      - si el nombre que devuelve la fuente coincide con el esperado (detecta instrumento equivocado);
      - moneda, bolsa y tipo de instrumento según la fuente;
      - la fecha REAL del último precio, no la que la fuente dice que es la actual;
      - si hay historial diario y si la fuente ofrece intradía para ese instrumento.

    Escribe resultados.csv y resumen.md en la carpeta -Salida.

.PARAMETER Entrada
    CSV de instrumentos. Columnas: Categoria,Simbolo,Isin,NombreEsperado,Mercado,Moneda,Notas

.PARAMETER Salida
    Carpeta donde dejar los resultados.

.PARAMETER DiasRetrasoAceptable
    Antigüedad máxima, en días naturales, antes de marcar el precio como retrasado. 4 por defecto
    (cubre un fin de semana más un festivo).

.PARAMETER PausaMs
    Pausa entre peticiones. No bajarla de 300 ms: es una fuente sin contrato y conviene no abusar.

.EXAMPLE
    pwsh -File .\Test-Cobertura.ps1 -Entrada .\instrumentos.csv -Salida .\resultados
#>
[CmdletBinding()]
param(
    [string] $Entrada,
    [string] $Salida,
    [int]    $DiasRetrasoAceptable = 4,
    [int]    $PausaMs = 400
)

# $PSScriptRoot NO es fiable dentro de los valores por defecto de param() cuando el script se lanza
# con -File: llega vacio y Join-Path falla. Por eso las rutas se resuelven aqui, ya cargado el script.
$raiz = $PSScriptRoot
if (-not $raiz) { $raiz = Split-Path -Parent $MyInvocation.MyCommand.Definition }
if (-not $Entrada) { $Entrada = Join-Path $raiz 'instrumentos.csv' }
if (-not $Salida) { $Salida = Join-Path $raiz 'resultados' }

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning ("Estas en PowerShell $($PSVersionTable.PSVersion). Se recomienda PowerShell 7 o " +
        "superior: Windows PowerShell 5.1 usa .NET Framework y en algunos equipos falla al negociar " +
        "TLS con esta fuente. Se intentara igualmente. " +
        "Para instalarlo:  winget install --id Microsoft.PowerShell")
}

# Fuerza TLS 1.2 (y 1.3 si el runtime lo soporta). En Windows PowerShell 5.1 es imprescindible.
try {
    $protocolos = [Net.SecurityProtocolType]::Tls12
    if ([Enum]::GetNames([Net.SecurityProtocolType]) -contains 'Tls13') {
        $protocolos = $protocolos -bor [Net.SecurityProtocolType]::Tls13
    }
    [Net.ServicePointManager]::SecurityProtocol = $protocolos
}
catch {
    Write-Verbose "No se pudo fijar el protocolo TLS: $($_.Exception.Message)"
}

# Para que la consola muestre bien los acentos. En PowerShell 7 no hace falta.
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

$BaseUrl = 'https://query1.finance.yahoo.com'
$Agente  = 'Mozilla/5.0 (prueba-cobertura-portafolio-personal)'

if (-not (Test-Path -LiteralPath $Entrada)) {
    throw "No encuentro el CSV de entrada: $Entrada"
}
if (-not (Test-Path -LiteralPath $Salida)) {
    New-Item -ItemType Directory -Path $Salida -Force | Out-Null
}

function Convertir-Texto {
    param([string] $Texto)
    if ([string]::IsNullOrWhiteSpace($Texto)) { return '' }
    $t = $Texto.ToLowerInvariant()
    $t = $t -replace '[áàäâ]', 'a' -replace '[éèëê]', 'e' -replace '[íìïî]', 'i'
    $t = $t -replace '[óòöô]', 'o' -replace '[úùüû]', 'u' -replace 'ñ', 'n'
    $t = $t -replace '[^a-z0-9 ]', ' '
    return ($t -replace '\s+', ' ').Trim()
}

function Test-Identidad {
    param([string] $Esperado, [string] $Devuelto)
    $e = Convertir-Texto $Esperado
    $d = Convertir-Texto $Devuelto
    if (-not $e -or -not $d) { return 'NO_VERIFICABLE' }
    if ($d -like "*$e*" -or $e -like "*$d*") { return 'SI' }
    $palabras = @($e.Split(' ') | Where-Object { $_.Length -ge 4 })
    if ($palabras.Count -eq 0) { return 'NO_VERIFICABLE' }
    $aciertos = @($palabras | Where-Object { $d -like "*$_*" }).Count
    if ($aciertos -ge [Math]::Max(1, [Math]::Ceiling($palabras.Count / 2))) { return 'SI' }
    if ($aciertos -ge 1) { return 'PARCIAL' }
    return 'NO'
}

function Invoke-Yahoo {
    param([string] $Url)
    $cabeceras = @{ 'User-Agent' = $Agente; 'Accept' = 'application/json' }
    return Invoke-RestMethod -Uri $Url -Headers $cabeceras -TimeoutSec 30
}

function Get-Candidatos {
    param([string] $Texto)
    $url = "$BaseUrl/v1/finance/search?q=$([uri]::EscapeDataString($Texto))&quotesCount=10&newsCount=0"
    $respuesta = Invoke-Yahoo $url
    return @($respuesta.quotes | Where-Object { $_.symbol })
}

function Get-Instrumento {
    param([string] $Simbolo, [string] $Rango = '1mo')
    $url = "$BaseUrl/v8/finance/chart/$([uri]::EscapeDataString($Simbolo))?range=$Rango&interval=1d&events=div%2Csplits"
    $respuesta = Invoke-Yahoo $url
    $chart = $respuesta.chart
    if ($null -eq $chart -or $null -eq $chart.result -or $chart.result.Count -eq 0) {
        $detalle = if ($chart.error) { $chart.error.description } else { 'sin resultado' }
        throw "La fuente no devolvió datos ($detalle)"
    }
    return $chart.result[0]
}

# ---------------------------------------------------------------- comprobacion previa

# Una sola peticion de prueba. Si esto falla no tiene sentido generar 60 filas de error: el problema
# es del entorno, no de los instrumentos.
try {
    Invoke-Yahoo "$BaseUrl/v8/finance/chart/AAPL?range=1d&interval=1d" | Out-Null
    Write-Host 'Conexion con la fuente: OK' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host 'No se puede conectar con la fuente de datos.' -ForegroundColor Red
    Write-Host "  Detalle: $($_.Exception.Message)" -ForegroundColor DarkGray
    Write-Host ''
    Write-Host 'Causas habituales, en orden de probabilidad:' -ForegroundColor Yellow
    Write-Host '  1. Politica de ejecucion de PowerShell: el script ni siquiera llega a cargarse.'
    Write-Host '     ("is not digitally signed" / "cannot be loaded")'
    Write-Host '     Diagnostico:                  Get-ExecutionPolicy -List'
    Write-Host '     Permiso solo para tu usuario: Set-ExecutionPolicy -Scope CurrentUser RemoteSigned'
    Write-Host '  2. Windows PowerShell 5.1 falla al negociar TLS con esta fuente.'
    Write-Host '     Usa PowerShell 7:  pwsh -ExecutionPolicy Bypass -File .\Test-Cobertura.ps1'
    Write-Host '     Si no lo tienes:   winget install --id Microsoft.PowerShell'
    Write-Host '  3. Sin salida a Internet, o una red corporativa con proxy.'
    Write-Host ''
    Write-Host "Version de PowerShell en uso: $($PSVersionTable.PSVersion) ($($PSVersionTable.PSEdition))"
    Write-Host 'Tambien puedes lanzarlo con:  .\ejecutar.cmd'
    exit 1
}

# ---------------------------------------------------------------- lectura

$instrumentos = Import-Csv -LiteralPath $Entrada
Write-Host "Instrumentos a comprobar: $($instrumentos.Count)" -ForegroundColor Cyan
Write-Host "Fuente: $BaseUrl (pública, sin clave)" -ForegroundColor DarkGray
Write-Host ''

$resultados = [System.Collections.Generic.List[object]]::new()
$numero = 0

foreach ($fila in $instrumentos) {

    $numero++
    $entrada = @($fila.Simbolo, $fila.Isin, $fila.NombreEsperado) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
    Write-Progress -Activity 'Comprobando cobertura' -Status "$entrada" `
        -PercentComplete ([Math]::Round(100 * $numero / $instrumentos.Count))

    $registro = [ordered]@{
        Categoria          = $fila.Categoria
        Entrada            = $entrada
        SimboloUsado       = ''
        ComoSeResolvio     = 'NoResuelto'
        NombreFuente       = ''
        Identidad          = 'NO_VERIFICABLE'
        Tipo               = ''
        Moneda             = ''
        Bolsa              = ''
        Precio             = ''
        FechaPrecio        = ''
        AntiguedadDias     = ''
        PuntosHistorial    = ''
        IntradiaDisponible = ''
        Veredicto          = 'ERROR'
        Observacion        = ''
    }

    try {
        # 1) Resolver el símbolo: directo, por ISIN o por nombre
        $simbolo = $fila.Simbolo
        if ([string]::IsNullOrWhiteSpace($simbolo) -and -not [string]::IsNullOrWhiteSpace($fila.Isin)) {
            $candidatos = Get-Candidatos $fila.Isin
            if ($candidatos.Count -gt 0) {
                $simbolo = $candidatos[0].symbol
                $registro.ComoSeResolvio = 'PorIsin'
                $registro.Observacion = "resuelto por ISIN; candidatos: " +
                    (($candidatos | Select-Object -First 4 | ForEach-Object { $_.symbol }) -join ', ')
            }
            Start-Sleep -Milliseconds $PausaMs
        }
        elseif ([string]::IsNullOrWhiteSpace($simbolo) -and -not [string]::IsNullOrWhiteSpace($fila.NombreEsperado)) {
            $candidatos = Get-Candidatos $fila.NombreEsperado
            if ($candidatos.Count -gt 0) {
                $simbolo = $candidatos[0].symbol
                $registro.ComoSeResolvio = 'PorNombre'
                $registro.Observacion = "resuelto por NOMBRE (revisar a mano); candidatos: " +
                    (($candidatos | Select-Object -First 4 | ForEach-Object { "$($_.symbol)=$($_.longname)" }) -join ' | ')
            }
            Start-Sleep -Milliseconds $PausaMs
        }
        else {
            $registro.ComoSeResolvio = 'Directo'
        }

        if ([string]::IsNullOrWhiteSpace($simbolo)) {
            $registro.Veredicto = 'NO_ENCONTRADO'
            $registro.Observacion = 'la fuente no devolvió ningún candidato'
            $resultados.Add([pscustomobject] $registro)
            continue
        }

        $registro.SimboloUsado = $simbolo

        # 2) Traer el instrumento
        $item = Get-Instrumento $simbolo
        Start-Sleep -Milliseconds $PausaMs

        $meta = $item.meta
        $registro.NombreFuente = if ($meta.longName) { $meta.longName } else { $meta.shortName }
        $registro.Tipo         = $meta.instrumentType
        $registro.Moneda       = $meta.currency
        $registro.Bolsa        = $meta.fullExchangeName
        $registro.Identidad    = Test-Identidad $fila.NombreEsperado $registro.NombreFuente

        $rangos = @($meta.validRanges)
        $registro.IntradiaDisponible = ($rangos -contains '1d')

        # 3) Leer la serie y quedarnos con el último cierre REAL, no con el que anuncia la fuente
        $cierres      = @($item.indicators.quote[0].close)
        $marcasTiempo = @($item.timestamp)
        $desfase      = [TimeSpan]::FromHours([int]$meta.gmtoffset)

        $ultimoNulo = $false
        if ($cierres.Count -gt 0 -and $null -eq $cierres[$cierres.Count - 1]) { $ultimoNulo = $true }

        $indice = $null
        for ($i = $cierres.Count - 1; $i -ge 0; $i--) {
            if ($null -ne $cierres[$i]) { $indice = $i; break }
        }

        $registro.PuntosHistorial = $cierres.Count

        # Eventos corporativos. Un split no invalida la cobertura, pero si la posicion se introdujo
        # con la cantidad y el coste anteriores al split, las cifras de esa posicion estaran mal.
        if ($item.events -and $item.events.splits) {
            $detalleSplits = @($item.events.splits.PSObject.Properties | ForEach-Object {
                $s = $_.Value
                $fechaSplit = [DateTimeOffset]::FromUnixTimeSeconds([long]$s.date).ToOffset($desfase).Date
                "$($s.splitRatio) el $($fechaSplit.ToString('yyyy-MM-dd'))"
            }) -join '; '
            if ($registro.Observacion) {
                $registro.Observacion = "$($registro.Observacion) | SPLIT: $detalleSplits - revisar cantidad y coste"
            }
            else {
                $registro.Observacion = "SPLIT: $detalleSplits - revisar cantidad y coste"
            }
        }

        if ($null -eq $indice) {
            $registro.Veredicto = 'SIN_PRECIO'
            $registro.Observacion = 'el instrumento existe pero no trae ningún cierre'
            $resultados.Add([pscustomobject] $registro)
            continue
        }

        $fechaPrecio = [DateTimeOffset]::FromUnixTimeSeconds([long]$marcasTiempo[$indice]).ToOffset($desfase).Date
        $registro.Precio       = [decimal]$cierres[$indice]
        $registro.FechaPrecio  = $fechaPrecio.ToString('yyyy-MM-dd')
        $antiguedad = ([DateTimeOffset]::Now.ToOffset($desfase).Date - $fechaPrecio).Days
        $registro.AntiguedadDias = $antiguedad

        # 4) Veredicto
        $esFondo = ($meta.instrumentType -eq 'MUTUALFUND')
        if ($registro.Identidad -in @('NO', 'PARCIAL')) {
            $registro.Veredicto = 'IDENTIDAD_DUDOSA'
            if (-not $registro.Observacion) {
                $registro.Observacion = 'el nombre devuelto no coincide con el esperado: verificar antes de usar'
            }
        }
        elseif ($ultimoNulo -and $esFondo) {
            $registro.Veredicto = 'NAV_PENDIENTE'
            $registro.Observacion = 'el último NAV aún no está publicado; se usaría el cierre anterior'
        }
        elseif ($antiguedad -gt $DiasRetrasoAceptable) {
            $registro.Veredicto = 'PRECIO_RETRASADO'
            $registro.Observacion = "último cierre de hace $antiguedad días"
        }
        elseif ($esFondo) {
            $registro.Veredicto = 'OK_FONDO_NAV'
            if (-not $registro.Observacion) { $registro.Observacion = 'NAV diario, sin intradía (esperado)' }
        }
        else {
            $registro.Veredicto = 'OK'
        }
    }
    catch {
        $registro.Veredicto = 'ERROR'
        $registro.Observacion = $_.Exception.Message
    }

    $resultados.Add([pscustomobject] $registro)
}

Write-Progress -Activity 'Comprobando cobertura' -Completed

# ---------------------------------------------------------------- informes

$rutaCsv = Join-Path $Salida 'resultados.csv'
$resultados | Export-Csv -LiteralPath $rutaCsv -NoTypeInformation -Encoding utf8

$orden = @{
    'IDENTIDAD_DUDOSA' = 0; 'NO_ENCONTRADO' = 1; 'ERROR' = 2; 'SIN_PRECIO' = 3
    'PRECIO_RETRASADO' = 4; 'NAV_PENDIENTE' = 5; 'OK_FONDO_NAV' = 6; 'OK' = 7
}
$lineas = [System.Collections.Generic.List[string]]::new()
$lineas.Add('# Resultados de la prueba de cobertura')
$lineas.Add('')
$lineas.Add("Ejecutada: $(Get-Date -Format 'yyyy-MM-dd HH:mm')  ")
$lineas.Add("Instrumentos comprobados: $($resultados.Count)  ")
$lineas.Add("Umbral de retraso: $DiasRetrasoAceptable días")
$lineas.Add('')
$lineas.Add('| Veredicto | Cuántos |')
$lineas.Add('|---|---|')
foreach ($g in ($resultados | Group-Object Veredicto | Sort-Object { $orden[$_.Name] })) {
    $lineas.Add("| $($g.Name) | $($g.Count) |")
}
$lineas.Add('')
$lineas.Add('## Detalle')
$lineas.Add('')
$lineas.Add('| Categoría | Entrada | Símbolo | Nombre que devuelve la fuente | Identidad | Tipo | Moneda | Bolsa | Precio | Fecha | Ver. | Observación |')
$lineas.Add('|---|---|---|---|---|---|---|---|---|---|---|---|')

foreach ($r in ($resultados | Sort-Object { $orden[$_.Veredicto] }, Categoria, Entrada)) {
    $lineas.Add("| $($r.Categoria) | $($r.Entrada) | $($r.SimboloUsado) | $($r.NombreFuente) | " +
        "$($r.Identidad) | $($r.Tipo) | $($r.Moneda) | $($r.Bolsa) | $($r.Precio) | $($r.FechaPrecio) | " +
        "$($r.Veredicto) | $($r.Observacion) |")
}

$rutaMd = Join-Path $Salida 'resumen.md'
$lineas | Set-Content -LiteralPath $rutaMd -Encoding utf8

Write-Host ''
Write-Host 'Resultado por veredicto:' -ForegroundColor Cyan
$resultados | Group-Object Veredicto | Sort-Object { $orden[$_.Name] } |
    ForEach-Object { Write-Host ("  {0,-18} {1}" -f $_.Name, $_.Count) }

Write-Host ''
Write-Host 'A revisar primero (identidad dudosa y no encontrados):' -ForegroundColor Yellow
$resultados | Where-Object { $_.Veredicto -in @('IDENTIDAD_DUDOSA', 'NO_ENCONTRADO', 'ERROR') } |
    ForEach-Object { Write-Host ("  [{0}] {1} -> {2}" -f $_.Veredicto, $_.Entrada, $_.Observacion) }

Write-Host ''
Write-Host "CSV:     $rutaCsv"
Write-Host "Resumen: $rutaMd"
