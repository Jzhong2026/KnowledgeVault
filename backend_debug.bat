@echo off
set ASPNETCORE_ENVIRONMENT=Development
cd /d D:\AI\Projects\KnowledgeVault\src\KnowledgeVault\KnowledgeVault
dotnet run --urls http://localhost:5030 > D:\AI\Projects\KnowledgeVault\backend_stdout.log 2>&1
