@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if exist "%~dp0dist\DestinyChatDesktop.exe" (
    start "" "%~dp0dist\DestinyChatDesktop.exe"
 ) else if exist "%~dp0DestinyChatDesktop.exe" (
    start "" "%~dp0DestinyChatDesktop.exe"
)
