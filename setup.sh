#!/bin/bash

set -e

echo "🚀 SkillForge Database Setup"
echo "=============================="

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker first."
    exit 1
fi

# Start PostgreSQL and Redis
echo "📦 Starting PostgreSQL and Redis..."
docker-compose up -d

# Wait for PostgreSQL to be ready
echo "⏳ Waiting for PostgreSQL to be ready..."
sleep 5

# Check if PostgreSQL is accepting connections
until docker exec skillforge-postgres pg_isready -U skillforge > /dev/null 2>&1; do
    echo "  Still waiting..."
    sleep 2
done

echo "✅ PostgreSQL is ready!"

# Check if dotnet is available
if ! command -v dotnet &> /dev/null; then
    echo "⚠️  Dotnet not found. Make sure to set DOTNET_ROOT:"
    echo "   export DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec"
    exit 1
fi

# Run database migrations
echo "🗄️  Running database migrations..."
cd backend/SkillForge.Api
export DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec
dotnet ef database update || echo "⚠️  Migration failed. You may need to run: dotnet ef migrations add InitialCreate"

echo ""
echo "✅ Setup complete!"
echo ""
echo "Database connection:"
echo "  Host: localhost"
echo "  Port: 5432"
echo "  User: skillforge"
echo "  Password: skillforge123"
echo "  Database: skillforge"
echo ""
echo "Start the backend:"
echo "  cd backend/SkillForge.Api"
echo "  dotnet run"
echo ""
echo "Start the frontend:"
echo "  cd frontend"
echo "  npm run dev"
