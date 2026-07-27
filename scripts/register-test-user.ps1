$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$LogDirectory = Join-Path $RepoRoot 'logs\scratch'
New-Item -ItemType Directory -Force $LogDirectory | Out-Null
$user  = 'kvtest'
$email = 'kvtest@local.dev'
$pw    = 'KvTest123!'
$out   = @()
$regBody = @{ UserName = $user; Email = $email; Password = $pw } | ConvertTo-Json
$registered = $false
for ($i = 0; $i -lt 24; $i++) {
    try {
        $r = Invoke-RestMethod -Uri 'http://localhost:5030/api/auth/register' -Method Post -ContentType 'application/json' -Body $regBody -TimeoutSec 8
        $out += "REGISTER_OK: UserName=$($r.User.UserName) Email=$($r.User.Email)"
        $registered = $true
        break
    } catch {
        $out += "ATTEMPT_$i FAIL: $_"
        Start-Sleep -Seconds 4
    }
}
if (-not $registered) {
    try {
        $lBody = @{ UserNameOrEmail = $email; Password = $pw } | ConvertTo-Json
        $l = Invoke-RestMethod -Uri 'http://localhost:5030/api/auth/login' -Method Post -ContentType 'application/json' -Body $lBody -TimeoutSec 8
        $out += "LOGIN_OK (already existed): UserName=$($l.User.UserName)"
    } catch {
        $out += "LOGIN_FAIL: $_"
    }
}
$out | Out-File (Join-Path $LogDirectory 'register-test-user.txt') -Encoding utf8
