# Comprueba que no haya datos personales en lo que se va a publicar.
#
# POR QUE EXISTE
# --------------
# El repositorio se publica, y dentro hay documentos y pruebas que narran casos reales. Anonimizar una vez
# no basta: cualquier linea nueva puede volver a colar un nombre o un numero de cuenta, y nadie lo notaria
# hasta que estuviera publicado y clonado. Esta comprobacion convierte «espero no haberme dejado ninguna»
# en «esta comprobado».
#
# QUE MIRA
# --------
# Los ficheros que DE VERDAD se publicarian. Si hay repositorio git, le pregunta a git —rastreados mas
# los que no estan ignorados—, que es la lista exacta. Si no lo hay, recorre el arbol aplicando las mismas
# exclusiones del .gitignore.
#
# Las cadenas a buscar viven en `.datos-sensibles.txt`, que esta excluido del repositorio: contiene justo
# lo que hay que mantener fuera, asi que publicarlo seria el propio fallo que intenta evitar.
#
# USO
# ---
#   ejecutar.cmd
# Devuelve 0 si esta limpio y 1 si encuentra algo.

$ErrorActionPreference = 'Stop'

# Raiz del proyecto: dos niveles por encima de esta carpeta.
$raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $raiz

$ficheroLista = Join-Path $raiz '.datos-sensibles.txt'

if (-not (Test-Path $ficheroLista)) {
    Write-Host '  No existe .datos-sensibles.txt: sin la lista no se puede comprobar.' -ForegroundColor Yellow
    Write-Host '  Se crea a partir de .datos-sensibles.example.txt y se rellena a mano.' -ForegroundColor Yellow
    exit 1
}

$cadenas = Get-Content $ficheroLista |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ -ne '' -and -not $_.StartsWith('#') }

if ($cadenas.Count -eq 0) {
    Write-Host '  La lista esta vacia: la comprobacion no comprobaria nada.' -ForegroundColor Yellow
    exit 1
}

Write-Host "  Cadenas a vigilar: $($cadenas.Count)"

# ---------------------------------------------------------------------------------------------
# Que ficheros se publicarian
# ---------------------------------------------------------------------------------------------
$extensionesTexto = @('.md', '.cs', '.mjs', '.js', '.ts', '.json', '.cmd', '.ps1', '.yml', '.yaml',
                      '.csproj', '.props', '.slnx', '.html', '.css', '.txt', '.csv', '.editorconfig')

$publicables = @()

if (Test-Path (Join-Path $raiz '.git')) {
    Write-Host '  Hay repositorio git: la lista sale de git, que es la exacta.'

    $rastreados = & git -C $raiz ls-files
    $sinIgnorar = & git -C $raiz ls-files --others --exclude-standard

    $publicables = @($rastreados) + @($sinIgnorar) |
        Where-Object { $_ -match '\S' } |
        Sort-Object -Unique |
        ForEach-Object { Join-Path $raiz $_ }
}
else {
    Write-Host '  Sin repositorio git: se recorre el arbol con las exclusiones del .gitignore.'

    $excluidas = '\\(bin|obj|node_modules|\.git|\.nuget|\.dotnet|\.tmp|\.scratch|\.herramientas|\.angular|dist|coverage|FichierTravail|salida|salida-2026)\\'

    $publicables = Get-ChildItem -Path $raiz -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -notmatch $excluidas -and
            $_.Extension -in $extensionesTexto -and
            $_.Name -ne '.datos-sensibles.txt'
        } |
        ForEach-Object { $_.FullName }
}

# El propio fichero de la lista nunca cuenta como hallazgo: contiene las cadenas a proposito.
$publicables = $publicables |
    Where-Object { $_ -and (Test-Path $_) -and (Split-Path $_ -Leaf) -ne '.datos-sensibles.txt' -and
                   $extensionesTexto -contains [System.IO.Path]::GetExtension($_) }

Write-Host "  Ficheros que se publicarian: $($publicables.Count)"
Write-Host ''

# ---------------------------------------------------------------------------------------------
# La comprobacion
# ---------------------------------------------------------------------------------------------
$hallazgos = 0

foreach ($cadena in $cadenas) {
    $encontrados = Select-String -Path $publicables -Pattern $cadena -SimpleMatch -ErrorAction SilentlyContinue

    if ($encontrados) {
        Write-Host "  ENCONTRADO: '$cadena'" -ForegroundColor Red

        foreach ($linea in $encontrados) {
            $relativa = $linea.Path.Replace($raiz + '\', '')
            Write-Host "      $relativa : $($linea.LineNumber)"
        }

        $hallazgos++
    }
}

Write-Host ''

if ($hallazgos -gt 0) {
    Write-Host "  HAY DATOS PERSONALES EN LO QUE SE VA A PUBLICAR: $hallazgos cadena(s)." -ForegroundColor Red
    Write-Host '  NO se publica hasta arreglarlo.' -ForegroundColor Red
    exit 1
}

Write-Host '  Limpio: ninguna de las cadenas aparece en lo que se publicaria.' -ForegroundColor Green
exit 0
