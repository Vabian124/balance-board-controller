@echo off
REM Build Balance Board Controller (Release). Requires .NET 8 SDK.
setlocal
cd /d "%~dp0"

echo Building Balance Board Controller...
dotnet build BalanceBoard.sln -c Release
if errorlevel 1 (
    echo.
    echo Build failed. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo.
echo Build OK:
echo   src\BalanceBoard.App\bin\Release\net8.0-windows\BalanceBoardApp.exe
echo.
echo Run: start.bat
pause
