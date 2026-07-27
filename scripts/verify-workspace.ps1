$ErrorActionPreference = 'Continue'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$TestDirectory = Join-Path $RepoRoot 'test\e2e'
New-Item -ItemType Directory -Force $TestDirectory | Out-Null
$Base = 'http://localhost:5030/api'
$Results = @()
$Dll = Join-Path $RepoRoot 'src\KnowledgeVault\KnowledgeVault\bin\Release\net10.0\KnowledgeVault.dll'
$ProjDir = Join-Path $RepoRoot 'src\KnowledgeVault\KnowledgeVault'
$TestDatabase = Join-Path $TestDirectory 'verify-workspace.db'
$SrvPid = $null

function Check($name, $cond, $detail = '') {
  $line = ("{0} | {1} | {2}" -f $(if($cond){'PASS'}else{'FAIL'}), $name, $detail)
  $script:Results += $line; Write-Host $line
}
function KillAllMine() {
  Get-Process -Name dotnet -ErrorAction SilentlyContinue |
    Where-Object { $_.Id -ne 42144 } |
    ForEach-Object { try { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue } catch {} }
}
function QS($hash) {
  $parts = @()
  foreach ($k in $hash.Keys) { $parts += ($k + '=' + [Uri]::EscapeDataString($hash[$k])) }
  return ($parts -join '&')
}

try {
  $psi = New-Object System.Diagnostics.ProcessStartInfo
  $psi.FileName = 'dotnet'
  $psi.Arguments = "`"$Dll`""
  $psi.WorkingDirectory = $ProjDir
  $psi.UseShellExecute = $false
  $psi.CreateNoWindow = $true
  $psi.EnvironmentVariables['ASPNETCORE_URLS'] = 'http://localhost:5030'
  $psi.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Development'
  $psi.EnvironmentVariables['ConnectionStrings__KnowledgeVaultDb'] = "Data Source=$TestDatabase"
  $proc = [System.Diagnostics.Process]::Start($psi)
  $SrvPid = $proc.Id
  Write-Host "Backend PID: $SrvPid (isolated db)"

  $ready = $false
  for ($i = 0; $i -lt 30; $i++) {
    $c = Test-NetConnection -ComputerName localhost -Port 5030 -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
    if ($c -and $c.TcpTestSucceeded) { $ready = $true; break }
    Start-Sleep -Seconds 1
  }
  Check 'backend listens on :5030' $ready
  if (-not $ready) { throw 'backend did not start' }

  $uname = "kv_e2e_$(Get-Date -Format yyyyMMddHHmmss)"
  $pw = 'E2ePass#123'
  Invoke-RestMethod -Uri "$Base/auth/register" -Method Post -ContentType 'application/json' -TimeoutSec 15 `
    -Body (ConvertTo-Json @{ UserName=$uname; Email="$uname@local.test"; Password=$pw }) | Out-Null
  Check 'register test user' $true
  $login = Invoke-RestMethod -Uri "$Base/auth/login" -Method Post -ContentType 'application/json' -TimeoutSec 15 `
    -Body (ConvertTo-Json @{ userNameOrEmail=$uname; password=$pw })
  $token = $login.accessToken
  Check 'login returns JWT' ($null -ne $token -and $token.Length -gt 10) ("len=" + ($token.Length))
  $h = @{ Authorization = "Bearer $token" }
  function NewFolder($body) { return Invoke-RestMethod -Uri "$Base/folders" -Method Post -ContentType 'application/json' -Headers $h -TimeoutSec 15 -Body (ConvertTo-Json $body) }
  $created = @()

  $root = NewFolder @{ scope='Personal'; name="ws-root-$(Get-Random)" }; $created += $root.id
  $child = NewFolder @{ scope='Personal'; parentFolderId=$root.id; name="ws-child-$(Get-Random)" }; $created += $child.id
  $grand = NewFolder @{ scope='Personal'; parentFolderId=$child.id; name="ws-grand-$(Get-Random)" }; $created += $grand.id
  Check 'create 3-level nested folders' ($grand.parentFolderId -eq $child.id -and $child.parentFolderId -eq $root.id)

  $uList = "$Base/folders?" + (QS @{ scope='Personal'; parentFolderId=$root.id; rootFolderId=$root.id })
  $content = Invoke-RestMethod -Uri $uList -Headers $h -TimeoutSec 15
  $uTree = "$Base/folders/tree?" + (QS @{ scope='Personal'; rootFolderId=$root.id })
  $tree = Invoke-RestMethod -Uri $uTree -Headers $h -TimeoutSec 15
  $childIdStr = [string]$child.id
  $childInList = ($content.folders | ForEach-Object { [string]$_.id }) -contains $childIdStr
  $childInTree = ($tree.children | ForEach-Object { [string]$_.id }) -contains $childIdStr
  Write-Host ("LIST_JSON=" + (ConvertTo-Json $content -Compress -Depth 5))
  Write-Host ("TREE_JSON=" + (ConvertTo-Json $tree -Compress -Depth 5))
  Check 'list content shows child' $childInList
  Check 'tree rooted at root shows child' ($tree.id -eq $root.id -and $childInTree)

  $b = NewFolder @{ scope='Personal'; name="ws-root-b-$(Get-Random)" }; $created += $b.id
  $uBoundary = "$Base/folders?" + (QS @{ scope='Personal'; parentFolderId=$b.id; rootFolderId=$root.id })
  $resp = try { Invoke-WebRequest -Uri $uBoundary -Headers $h -TimeoutSec 15 -UseBasicParsing -ErrorAction Stop } catch { $_.Exception.Response.StatusCode }
  Check 'boundary outside root -> 400' ($resp -eq 400) ("status=$resp")

  $doc = Invoke-RestMethod -Uri "$Base/documents" -Method Post -ContentType 'application/json' -Headers $h -TimeoutSec 15 `
    -Body (ConvertTo-Json @{ scope='Personal'; documentType='General'; title="ws-doc-$(Get-Random)"; content='e2e'; status='Active' })
  $docId = $doc.id
  Invoke-RestMethod -Uri "$Base/documents/$docId/folder" -Method Patch -ContentType 'application/json' -Headers $h -TimeoutSec 15 -Body (ConvertTo-Json @{ folderId=$root.id }) | Out-Null
  $uList2 = "$Base/folders?" + (QS @{ scope='Personal'; parentFolderId=$root.id; rootFolderId=$root.id })
  $content2 = Invoke-RestMethod -Uri $uList2 -Headers $h -TimeoutSec 15
  $movedOk = ($content2.documents | ForEach-Object { [string]$_.id }) -contains ([string]$docId)
  Write-Host ("LIST2_JSON=" + (ConvertTo-Json $content2 -Compress -Depth 5))
  Check 'move document into folder' $movedOk
  Invoke-RestMethod -Uri "$Base/documents/$docId/folder" -Method Patch -ContentType 'application/json' -Headers $h -TimeoutSec 15 -Body (ConvertTo-Json @{ folderId=$null }) | Out-Null
  Invoke-RestMethod -Uri "$Base/documents/$docId" -Method Delete -Headers $h -TimeoutSec 15 | Out-Null

  $p = NewFolder @{ scope='Personal'; name="ws-nonempty-$(Get-Random)" }; $created += $p.id
  $c2 = NewFolder @{ scope='Personal'; parentFolderId=$p.id; name="ws-nonempty-child-$(Get-Random)" }; $created += $c2.id
  $del = try { Invoke-WebRequest -Uri "$Base/folders/$($p.id)" -Method Delete -Headers $h -TimeoutSec 15 -UseBasicParsing -ErrorAction Stop } catch { $_.Exception.Response.StatusCode }
  Check 'delete non-empty folder -> 409' ($del -eq 409) ("status=$del")

  for ($i = $created.Count - 1; $i -ge 0; $i--) { try { Invoke-RestMethod -Uri "$Base/folders/$($created[$i])" -Method Delete -Headers $h -TimeoutSec 15 | Out-Null } catch {} }
  Write-Host "DATA_LAYER_DONE"
} catch {
  Check 'script completed without fatal error' $false $_.Exception.Message
} finally {
  try { if ($SrvPid) { Stop-Process -Id $SrvPid -Force -ErrorAction SilentlyContinue } } catch {}
  KillAllMine
  $fail = ($Results | Where-Object { $_.StartsWith('FAIL') }).Count
  Write-Host ("==== SUMMARY: " + ($Results.Count - $fail) + " passed, " + $fail + " failed ====")
}
