$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetRoot = Join-Path $scriptDir "dotnet-sdk"
$dotnetExe = Join-Path $dotnetRoot "dotnet.exe"

if (-not (Test-Path $dotnetExe)) {
    throw "未找到本地 .NET SDK：$dotnetExe"
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"

& $dotnetExe @args
exit $LASTEXITCODE
