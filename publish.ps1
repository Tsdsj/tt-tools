$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'src\TtLauncher\TtLauncher.csproj'
$outputPath = Join-Path $PSScriptRoot 'publish\win-x64'
$dotnetScript = Join-Path $PSScriptRoot 'dotnet-local.ps1'

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if (-not (Test-Path $dotnetScript)) {
    throw "Local dotnet script not found: $dotnetScript"
}

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

Write-Host 'Publishing TtLauncher beta-0.0.1...' -ForegroundColor Cyan

powershell -ExecutionPolicy Bypass -File $dotnetScript publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $outputPath

Write-Host ''
Write-Host 'Publish completed.' -ForegroundColor Green
Write-Host $outputPath
Write-Host ''
Write-Host 'Verify these files before shipping:' -ForegroundColor Yellow
Write-Host "1. $outputPath\\TtLauncher.exe"
Write-Host "2. $outputPath\\tools\\everything\\es.exe"
Write-Host "3. $outputPath\\tessdata\\eng.traineddata"
Write-Host "4. $outputPath\\tessdata\\chi_sim.traineddata"
