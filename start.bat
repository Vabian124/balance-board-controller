@echo off
REM Double-click to launch Balance Board Controller (builds first if needed).
setlocal
cd /d "%~dp0"

set BALANCEBOARD_DEV=1
set EXE=src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe

if exist "%EXE%" (
    echo Starting Balance Board Controller...
    start "" "%CD%\%EXE%" --dev
    exit /b 0
)

echo Release build not found. Building...
dotnet build BalanceBoard.sln -c Release
if errorlevel 1 (
    echo Build failed. Install .NET 8 SDK: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Starting...
start "" "%CD%\%EXE%" --dev
