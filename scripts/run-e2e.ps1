$ErrorActionPreference = 'Continue'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$TestDirectory = Join-Path $RepoRoot 'test\e2e'
New-Item -ItemType Directory -Force $TestDirectory | Out-Null
$backendDll = Join-Path $RepoRoot 'src\KnowledgeVault\KnowledgeVault\bin\Release\net10.0\KnowledgeVault.dll'
$webDir = Join-Path $RepoRoot 'src\knowledge-vault-web'
$testDatabase = Join-Path $TestDirectory 'e2e.db'
$e2eOutput = Join-Path $TestDirectory 'playwright-output.txt'
$backendPid = $null

function Kill-OccupantsOfPort($port) {
    try {
        $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        foreach ($c in $conns) { try { Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue } catch {} }
    } catch {}
}

try {
    # --- Preflight: free :5030 (backend) and :4200 (ng serve) ---
    Kill-OccupantsOfPort 5030
    Kill-OccupantsOfPort 4200
    Start-Sleep -Seconds 1
    # --- Start backend (Release DLL) on :5030 ---
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'dotnet'
    $psi.Arguments = "`"$backendDll`""
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.EnvironmentVariables['ASPNETCORE_URLS'] = 'http://localhost:5030'
    $psi.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $psi.EnvironmentVariables['ConnectionStrings__KnowledgeVaultDb'] = "Data Source=$testDatabase"
    $p = [System.Diagnostics.Process]::Start($psi)
    $backendPid = $p.Id
    Write-Host "Backend started (pid=$backendPid)"

    # --- Wait for :5030 ---
    $ready = $false
    for ($i = 0; $i -lt 40; $i++) {
        if (Test-NetConnection -ComputerName localhost -Port 5030 -InformationLevel Quiet) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { Write-Host 'ERROR: backend did not start on :5030'; exit 1 }

    # --- Provision a unique test user ---
    $user = "e2e-$(Get-Date -Format yyyyMMddHHmmss)-$(Get-Random -Maximum 9999)"
    $email = "$user@example.com"
    $pw = 'E2ePass123!'
    $body = @{ UserName = $user; Email = $email; Password = $pw } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri 'http://localhost:5030/api/auth/register' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 20 | Out-Null
        Write-Host "Registered test user: $user"
    } catch {
        Write-Host "Register note: $_  (will still attempt login)"
    }

    # --- Run Playwright e2e (it manages ng serve on :4200 itself) ---
    $env:KV_TEST_USER = $user
    $env:KV_TEST_PASSWORD = $pw
    Set-Location $webDir
    npx playwright test e2e/workspace.spec.ts --reporter=list > $e2eOutput 2>&1
    $e2eExit = $LASTEXITCODE
    Write-Host "E2E exit code: $e2eExit"
    Get-Content $e2eOutput | Select-Object -Last 40
    exit $e2eExit
} finally {
    if ($backendPid) {
        try { Stop-Process -Id $backendPid -Force -ErrorAction SilentlyContinue } catch {}
        Write-Host "Backend stopped (pid=$backendPid)"
    }
}
