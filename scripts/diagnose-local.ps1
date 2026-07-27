$ErrorActionPreference = 'Continue'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Dll = Join-Path $RepoRoot 'src\KnowledgeVault\KnowledgeVault\bin\Release\net10.0\KnowledgeVault.dll'
$ProjDir = Join-Path $RepoRoot 'src\KnowledgeVault\KnowledgeVault'
$LogDirectory = Join-Path $RepoRoot 'logs\scratch'
$TestDirectory = Join-Path $RepoRoot 'test\e2e'
New-Item -ItemType Directory -Force $LogDirectory | Out-Null
New-Item -ItemType Directory -Force $TestDirectory | Out-Null
$log = Join-Path $LogDirectory 'diagnose-local.log'
$testDatabase = Join-Path $TestDirectory 'diagnose-local.db'
Remove-Item -Force $log -ErrorAction SilentlyContinue
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = 'cmd'
$psi.Arguments = "/c `"cd /d `"$ProjDir`" && set ASPNETCORE_URLS=http://localhost:5030 && set ASPNETCORE_ENVIRONMENT=Development && set `"ConnectionStrings__KnowledgeVaultDb=Data Source=$testDatabase`" && dotnet `"$Dll`" > `"$log`" 2>&1`""
$psi.UseShellExecute = $true
$psi.WindowStyle = 'Hidden'
$proc = [System.Diagnostics.Process]::Start($psi)
Write-Host "Launched PID $($proc.Id); waiting 25s for startup log..."
Start-Sleep -Seconds 25
Write-Host "=== probing 5030 ==="
try { $r = Invoke-WebRequest -Uri "http://localhost:5030/KnowledgeVault/api/auth/me" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop; Write-Host "5030 HTTP $($r.StatusCode)" } catch { Write-Host "5030 not ready" }
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne 42144 } | ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch {} }
Write-Host "DIAG_DONE"
