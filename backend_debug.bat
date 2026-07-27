@echo off
set ASPNETCORE_ENVIRONMENT=Development
cd /d E:\Projects\KnowledgeVault\src\KnowledgeVault\KnowledgeVault
dotnet run --urls http://localhost:5030 > E:\Projects\KnowledgeVault\backend_stdout.log 2>&1
