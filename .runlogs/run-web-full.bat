@echo off
cd /d "e:\Projects\KnowledgeVault\src\knowledge-vault-web"
set "PATH=C:\Users\jason\.workbuddy\binaries\node\versions\22.22.2;C:\Users\jason\.dotnet10;%PATH%"
call npm install > "e:\Projects\KnowledgeVault\.runlogs\web.out.log" 2>&1
if errorlevel 1 ( echo NPM_INSTALL_FAILED >> "e:\Projects\KnowledgeVault\.runlogs\web.out.log" & exit /b 1 )
call npm start >> "e:\Projects\KnowledgeVault\.runlogs\web.out.log" 2>&1
