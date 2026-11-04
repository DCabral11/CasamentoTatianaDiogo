@echo off
set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=http://127.0.0.1:5000
set CASAMENTO_SETTINGS_PATH=C:\ProgramData\CasamentoTatianaDiogo\settings\appsettings.Local.json
set DATA_PROTECTION_KEYS_PATH=C:\ProgramData\CasamentoTatianaDiogo\keys

"%ProgramFiles%\dotnet\dotnet.exe" "%~dp0CasamentoTatianaDiogo.dll"
