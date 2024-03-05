#!/bin/bash

# Stop and remove containers
docker compose -f docker-compose-bridge.yml down

# Build and start containers in detached mode
docker compose -f docker-compose-bridge.yml up -d --build
