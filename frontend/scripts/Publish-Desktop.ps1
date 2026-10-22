param()

$ErrorActionPreference = 'Stop'
$repoPath = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$desktopProject = Join-Path $repoPath 'frontend/Delivergate.Desktop/Delivergate.Desktop.csproj'
$checksProject = Join-Path $repoPath 'frontend/Delivergate.ConnectionChecks/Delivergate.ConnectionChecks.csproj'
$releaseRoot = Join-Path $repoPath 'frontend/artifacts'
# A new directory on every run avoids overwriting a previous package.
$releaseName = 'Delivergate-Desktop-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$releasePath = Join-Path $releaseRoot $releaseName

Push-Location (Join-Path $repoPath 'frontend')
try {
    & dotnet run --project $checksProject -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Frontend checks failed. No package was published.' }

    & dotnet build $desktopProject -c Release -t:Rebuild
    if ($LASTEXITCODE -ne 0) { throw 'Desktop rebuild failed. No package was published.' }

    & dotnet publish $desktopProject -c Release --no-build --self-contained false -o $releasePath
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed. Do not distribute the incomplete output.' }

    foreach ($requiredFile in @('Delivergate.Desktop.exe', 'Delivergate.Desktop.dll', 'Delivergate.Desktop.runtimeconfig.json', 'appsettings.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $releasePath $requiredFile))) {
            throw "Required package file is missing: $requiredFile"
        }
    }
    $settings = Get-Content -LiteralPath (Join-Path $releasePath 'appsettings.json') -Raw | ConvertFrom-Json
    if ($settings.ApiBaseUrl -ne 'http://localhost:3000/') {
        throw 'Release configuration must point to localhost:3000. Review appsettings.json before distributing.'
    }
    $sensitiveFiles = Get-ChildItem -LiteralPath $releasePath -Recurse -Force -File |
        Where-Object { $_.Name -like '.env*' -or $_.Extension -eq '.db' -or $_.Name -like '*.db-*' }
    if ($sensitiveFiles) { throw 'Unexpected environment or database files found in publish output.' }

    Copy-Item -LiteralPath (Join-Path $repoPath 'frontend/RUN-DESKTOP.md') -Destination $releasePath
    $archivePath = "$releasePath.zip"
    Compress-Archive -Path (Join-Path $releasePath '*') -DestinationPath $archivePath
    Write-Host "Package: $archivePath"
    Write-Host "SHA256: $((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash)"
    Write-Host 'Automated checks passed. Manual UI and live CRUD acceptance are separate checks.'
} finally {
    Pop-Location
}
