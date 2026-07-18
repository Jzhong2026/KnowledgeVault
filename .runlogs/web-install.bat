@echo off
cd /d "e:\Projects\KnowledgeVault\src\knowledge-vault-web"
set "PATH=C:\Users\jason\.workbuddy\binaries\node\versions\22.22.2;C:\Users\jason\.dotnet10;%PATH%"
call npm install > "e:\Projects\KnowledgeVault\.runlogs\web-install.log" 2>&1
