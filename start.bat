@echo off
REM Double-click to launch Balance Board Controller (builds first if needed).
setlocal
cd /d "%~dp0"

set EXE=src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe

if not exist "%EXE%" (
    echo Release build not found. Building...
    dotnet build BalanceBoard.sln -c Release
    if errorlevel 1 (
        echo Build failed. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
        pause
        exit /b 1
    )
)

echo Starting Balance Board Controller v1.5.2 (stable)...
start "" "%CD%\%EXE%"
