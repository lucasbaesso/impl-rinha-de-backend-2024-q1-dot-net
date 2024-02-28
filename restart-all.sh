#!/bin/bash
# enter app directory and compile dotnet
cd app
dotnet publish -r linux-x64 -c Release --self-contained true

# enter root directory
cd ..

# Stop and remove containers
docker compose down

# Build and start containers in detached mode
docker compose up -d --build
