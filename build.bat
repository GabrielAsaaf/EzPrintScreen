@echo off
echo ========================================
echo  EzPrintScreen - Build Script
echo ========================================
echo.

where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [ERROR] .NET 8 SDK not found!
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo [1/2] Building project...
dotnet build EzPrintScreen.csproj -c Release --nologo -v q
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Build failed!
    pause
    exit /b 1
)

echo [2/2] Publishing single-file executable...
dotnet publish EzPrintScreen.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish --nologo -v q
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Publish failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Build successful!
echo  Output: publish\EzPrintScreen.exe
echo ========================================
pause
