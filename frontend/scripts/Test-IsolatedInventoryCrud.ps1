param([switch]$IncludeCustomers)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('delivergate-f6-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$keys = @('PORT', 'DATABASE_PATH', 'JWT_SECRET', 'OWNER_PASSWORD', 'MANAGER_PASSWORD', 'CASHIER_PASSWORD', 'F6_TEST_API_URL')
$previous = @{}
foreach ($key in $keys) { $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process') }
$backend = $null
Push-Location $repoRoot
try {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $testPort = $listener.LocalEndpoint.Port
    $listener.Stop()
    $testPassword = [Guid]::NewGuid().ToString('N') + '!Test'
    [Environment]::SetEnvironmentVariable('PORT', [string]$testPort, 'Process')
    [Environment]::SetEnvironmentVariable('DATABASE_PATH', (Join-Path $testRoot 'test.db'), 'Process')
    [Environment]::SetEnvironmentVariable('JWT_SECRET', [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N'), 'Process')
    foreach ($key in @('OWNER_PASSWORD', 'MANAGER_PASSWORD', 'CASHIER_PASSWORD')) {
        [Environment]::SetEnvironmentVariable($key, $testPassword, 'Process')
    }
    $url = "http://127.0.0.1:$testPort/"
    [Environment]::SetEnvironmentVariable('F6_TEST_API_URL', $url, 'Process')
    $executable = Join-Path $testRoot 'delivergate-test.exe'
    & go build -o $executable .
    if ($LASTEXITCODE -ne 0) { throw 'Test backend build failed.' }
    $backend = Start-Process -FilePath $executable -WorkingDirectory $repoRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $testRoot 'server.log') -RedirectStandardError (Join-Path $testRoot 'server-error.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($backend.HasExited) { throw 'Isolated backend exited before readiness.' }
        try {
            $health = Invoke-RestMethod -Uri ($url + 'health') -TimeoutSec 1
            if ($health.status -eq 'ok') { $ready = $true; break }
        } catch { Start-Sleep -Milliseconds 200 }
    }
    if (!$ready) { throw 'Isolated backend did not become ready.' }
    $checkArguments = @('--live-crud')
    if ($IncludeCustomers) { $checkArguments += '--live-customers' }
    & dotnet run --project './frontend/Delivergate.ConnectionChecks/Delivergate.ConnectionChecks.csproj' -- @checkArguments
    if ($LASTEXITCODE -ne 0) { throw 'Inventory CRUD checks failed.' }
    Write-Output 'Isolated CRUD checks passed; the real database was not used.'
} finally {
    if ($backend -and !$backend.HasExited) { Stop-Process -Id $backend.Id }
    foreach ($key in $keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
    Pop-Location
    Write-Output "Temporary test database and logs retained at: $testRoot"
}
