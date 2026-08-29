@echo off
setlocal EnableDelayedExpansion

set "DOTNET_FOUND=0"
for /f "usebackq tokens=*" %%R in (`dotnet --list-runtimes 2^>nul`) do (
    echo %%R | findstr /I /R /C:"Microsoft\.NETCore\.App 9\." >nul 2>&1 && set "DOTNET_FOUND=1"
)

if "%DOTNET_FOUND%"=="1" (
    start "" "%~dp0SpaceMaker.exe"
    goto :EOF
)

echo.
echo  SpaceMaker requires the .NET 9 Runtime, which was not detected on this PC.
echo  Opening the official download page now. Please install it and run this launcher again.
echo.
start "" "https://dotnet.microsoft.com/download/dotnet/9.0"
pause
