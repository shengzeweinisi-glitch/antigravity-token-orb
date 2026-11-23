@echo off
cd /d "%~dp0"
if not exist "AntigravityTokenOrb.exe" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
    if errorlevel 1 pause & exit /b 1
)
start "" "%~dp0AntigravityTokenOrb.exe"
