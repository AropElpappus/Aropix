param([int]$Repeticiones = 3)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($Repeticiones -lt 1 -or $Repeticiones -gt 3) { throw 'Usa entre 1 y 3 repeticiones.' }
& (Join-Path $PSScriptRoot 'compilar.cmd')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$runRoot = Join-Path $projectRoot ('artifacts\history\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$previousRoot = $env:AROPIX_TEST_OUTPUT_ROOT
$failed = 0
try {
    $env:AROPIX_TEST_OUTPUT_ROOT = $runRoot
    $exe = Join-Path $projectRoot 'bin\Release\PixelArt.exe'
    @("Fecha: $(Get-Date -Format o)", "OS: $([Environment]::OSVersion)",
      "Procesadores lógicos: $([Environment]::ProcessorCount)",
      "Repeticiones por escenario: $Repeticiones", 'Procesos secuenciales, máximo 60 segundos por caso.') |
        Set-Content -LiteralPath (Join-Path $runRoot 'entorno.txt')
    $cases = @()
    foreach ($size in @(64,256,512,1024,2048)) {
        foreach ($layers in @(1,4,16)) { $cases += "--history-diagnostic $size $layers" }
    }
    $cases += '--history-diagnostic multi'
    $cases += '--history-diagnostic budget'
    for ($repeat = 1; $repeat -le $Repeticiones; $repeat++) {
        foreach ($arguments in $cases) {
            $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
            if (-not $process.WaitForExit(60000)) {
                # Kill only this diagnostic child, never an editor belonging to the user.
                $process.Kill(); $process.WaitForExit(); $failed++
                "TIMEOUT | $repeat | $arguments" | Add-Content (Join-Path $runRoot 'ejecuciones.txt')
                throw 'El caso excedió 60 segundos; se detiene la medición.'
            }
            $process.Refresh()
            "$($process.ExitCode) | $repeat | $arguments" | Add-Content (Join-Path $runRoot 'ejecuciones.txt')
            if ($process.ExitCode -ne 0) { $failed++ }
            Write-Output "Repeticion $repeat : $arguments -> $($process.ExitCode)"
            $process.Dispose()
            if ($failed) { throw 'Caso omitido por seguridad o fallido; revisa error.txt. No se ejecutan casos mayores.' }
        }
    }
    Get-ChildItem -LiteralPath $runRoot -Recurse -Filter history.csv |
        ForEach-Object { Import-Csv -LiteralPath $_.FullName } |
        Export-Csv -LiteralPath (Join-Path $runRoot 'resumen.csv') -NoTypeInformation
    Get-ChildItem -LiteralPath $runRoot -Recurse -Filter multi.csv |
        ForEach-Object { Import-Csv -LiteralPath $_.FullName } |
        Export-Csv -LiteralPath (Join-Path $runRoot 'multiproyecto.csv') -NoTypeInformation
    Get-ChildItem -LiteralPath $runRoot -Recurse -Filter budget.csv |
        ForEach-Object { Import-Csv -LiteralPath $_.FullName } |
        Export-Csv -LiteralPath (Join-Path $runRoot 'presupuesto.csv') -NoTypeInformation
}
finally {
    $env:AROPIX_TEST_OUTPUT_ROOT = $previousRoot
    Write-Output "Mediciones: $runRoot"
}
