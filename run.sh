#!/bin/bash
set -e

# Change to the directory where the script is located
cd "$(dirname "$0")"

echo "========================================"
echo "Building Solution..."
echo "========================================"
dotnet build FindFiles/FindFiles.csproj

echo ""
echo "========================================"
echo "Running Tests..."
echo "========================================"
dotnet test FindFiles.Tests/FindFiles.Tests.csproj

echo ""
echo "========================================"
echo "Starting Application..."
echo "========================================"
dotnet run --project FindFiles/FindFiles.csproj
