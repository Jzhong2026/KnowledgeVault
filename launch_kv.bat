@echo off
REM Free ports 5030 and 4200 if occupied
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":5030 " ^| findstr LISTENING') do taskkill /f /pid %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":4200 " ^| findstr LISTENING') do taskkill /f /pid %%a >nul 2>&1
timeout /t 3 >nul
start "KVBackend" cmd /c e:\Projects\KnowledgeVault\backend_debug.bat
start "KVFrontend" cmd /c e:\Projects\KnowledgeVault\frontend_debug.bat
exit
