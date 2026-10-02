#!/bin/bash
# Builds the app and runs the generated unit + BDD tests inside the .NET 8 SDK image.
# Integration tests (Category=Integration) need Docker/Testcontainers and run separately.
set -e
echo "Running .NET build and tests inside Docker..."
docker run --rm -v "$(pwd)":/app -w /app -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 mcr.microsoft.com/dotnet/sdk:8.0 \
  sh -c 'dotnet build tests/*/*.Tests.csproj -nologo -v q && dotnet test tests/*/*.Tests.csproj -nologo -v q --no-build --filter "Category!=Integration"'
