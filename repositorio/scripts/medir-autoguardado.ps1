$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'compilar.cmd')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$runRoot = Join-Path $projectRoot ('artifacts\autosave\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$previousRoot = $env:AROPIX_TEST_OUTPUT_ROOT
try {
    $env:AROPIX_TEST_OUTPUT_ROOT = $runRoot
    @("Fecha: $(Get-Date -Format o)", "OS: $([Environment]::OSVersion)",
      "CPU logicos: $([Environment]::ProcessorCount)", '3 muestras por escenario; procesos secuenciales; timeout 120s; datos sinteticos.') |
        Set-Content (Join-Path $runRoot 'entorno.txt')
    foreach ($scenario in @('safety','small','medium','large','noise','multi','unsaved','clean','slow')) {
        $process = Start-Process -FilePath (Join-Path $projectRoot 'bin\Release\PixelArt.exe') -ArgumentList "--autosave-diagnostic $scenario" -WindowStyle Hidden -PassThru
        try {
            if (-not $process.WaitForExit(120000)) {
                $process.Kill(); $process.WaitForExit()
                throw "Timeout del proceso de diagnostico: $scenario"
            }
            $process.Refresh()
            "$scenario : $($process.ExitCode)" | Tee-Object -FilePath (Join-Path $runRoot 'ejecuciones.txt') -Append
            if ($process.ExitCode -ne 0) { throw 'Diagnostico fallido; consulta error.txt. Se detienen los casos restantes.' }
        } finally { $process.Dispose() }
    }
    Get-ChildItem -LiteralPath $runRoot -Recurse -Filter autosave.csv |
        ForEach-Object { Import-Csv -LiteralPath $_.FullName } |
        Export-Csv -LiteralPath (Join-Path $runRoot 'resumen.csv') -NoTypeInformation
} finally {
    $env:AROPIX_TEST_OUTPUT_ROOT = $previousRoot
    Write-Output "Resultados: $runRoot"
}
