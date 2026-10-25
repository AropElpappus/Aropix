param([string]$Proyecto)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'compilar.cmd')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$testRoot = Join-Path $projectRoot ('artifacts\tests\' + [Guid]::NewGuid().ToString('N'))
$previousRoot = $env:AROPIX_TEST_OUTPUT_ROOT
try {
    $env:AROPIX_TEST_OUTPUT_ROOT = $testRoot
    $arguments = @('--test')
    if ($Proyecto) {
        $inputProject = (Resolve-Path -LiteralPath $Proyecto).Path
        $arguments += '"' + $inputProject + '"'
    }
    $exe = Join-Path $projectRoot 'bin\Release\PixelArt.exe'
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    Get-ChildItem -LiteralPath $testRoot -Recurse -Filter resultados.txt | ForEach-Object {
        Get-Content -LiteralPath $_.FullName
    }
    Write-Output "Resultados y capturas: $testRoot"
    exit $process.ExitCode
}
finally { $env:AROPIX_TEST_OUTPUT_ROOT = $previousRoot }
