@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1"
echo.
if exist "%~dp0DestinyChatDesktop.exe" (
    echo EXE ready at:
    echo %~dp0DestinyChatDesktop.exe
) else (
    echo Build finished, but the EXE was not found in the main folder.
)
pause
