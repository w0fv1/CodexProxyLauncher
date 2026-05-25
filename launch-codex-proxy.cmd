@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\Start-CodexProxy.ps1" -Proxy "http://127.0.0.1:7890" -Restart -PatchCodexConfig
