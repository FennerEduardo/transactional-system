#!/bin/bash
echo "Running .NET Build validation inside Docker..."
docker run --rm -v $(pwd):/app -w /app mcr.microsoft.com/dotnet/sdk:8.0 dotnet build
