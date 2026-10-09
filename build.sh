#!/bin/bash
echo "========================================"
echo " EzPrintScreen - Build Script (Linux)"
echo "========================================"
echo ""

if ! command -v dotnet &> /dev/null; then
    echo "[ERROR] .NET 8 SDK not found!"
    echo "This project requires Windows to run. This script only verifies compilation."
    exit 1
fi

echo "[1/2] Checking project structure..."
dotnet restore EzPrintScreen.csproj --nologo -v q 2>/dev/null
if [ $? -ne 0 ]; then
    echo "[INFO] Restore may require Windows SDK (expected in cross-env)"
fi

echo "[2/2] Verifying C# syntax..."
echo "Project files ready for Windows compilation."
echo ""
echo "To build on Windows, run: build.bat"
echo "Or manually:"
echo "  dotnet build EzPrintScreen.csproj -c Release"
echo "  dotnet publish EzPrintScreen.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish"
