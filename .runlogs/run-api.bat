@echo off
cd /d "e:\Projects\KnowledgeVault\src\KnowledgeVault\KnowledgeVault"
set "KV_DB_CONNECTION=Data Source=knowledge-vault.db"
set "KV_JWT_SIGNING_KEY=dev-local-only-signing-key-change-me-1234567890"
set "ASPNETCORE_ENVIRONMENT=Development"
set "PATH=C:\Users\jason\.dotnet10;C:\Users\jason\.workbuddy\binaries\node\versions\22.22.2;%PATH%"
"C:\Users\jason\.dotnet10\dotnet.exe" run --project KnowledgeVault.csproj --launch-profile http > "e:\Projects\KnowledgeVault\.runlogs\api.out.log" 2>&1
