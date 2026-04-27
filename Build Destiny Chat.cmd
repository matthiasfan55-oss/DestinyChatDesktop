@echo off
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1"
echo.
if exist "%~dp0dist\DestinyChatDesktop.exe" (
    echo Fresh EXE ready at:
    echo %~dp0dist\DestinyChatDesktop.exe
) else if exist "%~dp0DestinyChatDesktop.exe" (
    echo EXE ready at:
    echo %~dp0DestinyChatDesktop.exe
) else (
    echo Build finished, but no EXE was found in dist or the main folder.
)
pause
