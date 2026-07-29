#!/bin/bash
set -e

echo "🚀 Iniciando deploy do Sentinela..."

# Check prerequisites
command -v docker >/dev/null 2>&1 || { echo "Docker is required"; exit 1; }
command -v docker-compose >/dev/null 2>&1 || { echo "Docker Compose is required"; exit 1; }

# Load environment
if [ -f .env ]; then
    export $(cat .env | grep -v '#' | xargs)
fi

# Pull latest images
docker-compose pull

# Build and start
docker-compose up -d --build

# Wait for health checks
echo "⏳ Aguardando serviços iniciarem..."
sleep 10

# Check status
docker-compose ps

echo "✅ Deploy concluído!"
echo "🌐 Acesse: https://sentinela.local"
echo "📊 Grafana: http://localhost:3001 (admin/${GRAFANA_PASSWORD:-admin})"
echo "📨 RabbitMQ: http://localhost:15672 (${RABBITMQ_USER:-sentinela})"
echo "📝 Seq: http://localhost:5341"
