#!/bin/bash

# Stop and remove containers
docker compose down

# Build and start containers in detached mode
docker compose up -d --build
