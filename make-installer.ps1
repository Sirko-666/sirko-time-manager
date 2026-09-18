param(
    [bool]$SelfContained = $true,
    [switch]$Mini
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($Mini) { $SelfContained = $false }

Write-Host "== 1. Publish TimerApp (Release, win-x64, self-contained=$SelfContained) =="
dotnet publish src\TimerApp -c Release -r win-x64 `
    --self-contained $SelfContained -o build\pack\app
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

Write-Host "== 2. Prune dev artifacts =="
Get-ChildItem "build\pack\app" -File | Where-Object {
    $_.Extension -in ".pdb", ".xml", ".ista"
} | Remove-Item -Force

Write-Host "== 3. Pack into zip =="
$packZip = "src\StmInstaller\pack.zip"
if (Test-Path $packZip) { Remove-Item $packZip -Force }
Compress-Archive -Path "build\pack\app\*" -DestinationPath $packZip -CompressionLevel Optimal

Write-Host "== 4. Build installer (single-file) with embedded pack =="
dotnet publish src\StmInstaller -c Release -r win-x64 `
    --self-contained $SelfContained -p:PublishSingleFile=true -o build\setup
if ($LASTEXITCODE -ne 0) { throw "installer build failed" }

$finalName = "build\STM-Setup.exe"
if ($Mini) { $finalName = "build\STM-Setup-Mini.exe" }
Copy-Item "build\setup\StmInstaller.exe" $finalName -Force
if (Test-Path $packZip) { Remove-Item $packZip -Force }

Write-Host "== Done: $finalName =="
