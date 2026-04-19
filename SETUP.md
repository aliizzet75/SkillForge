# SkillForge Setup

## Quick Start

### 1. Datenbank starten
```bash
./setup.sh
```

Dies startet PostgreSQL und Redis in Docker.

### 2. Backend starten
```bash
cd backend/SkillForge.Api
export DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec
dotnet run
```

### 3. Frontend starten
```bash
cd frontend
npm install
npm run dev
```

### 4. Öffne im Browser
http://localhost:3000

## Konfiguration

### OAuth Credentials

In `backend/SkillForge.Api/appsettings.json` eintragen:

**Google:**
- https://console.cloud.google.com/apis/credentials
- Erstelle OAuth 2.0 Client ID
- Redirect URI: `http://localhost:5001/api/auth/google-callback`

**Facebook:**
- https://developers.facebook.com/apps
- Erstelle App
- OAuth Redirect: `http://localhost:5001/api/auth/facebook-callback`

### JWT Key

Generiere einen sicheren Key:
```bash
openssl rand -base64 32
```

Füge ihn in `appsettings.json` ein:
```json
"Jwt": {
  "Key": "dein-generierter-key-hier",
  "Issuer": "SkillForge",
  "Audience": "SkillForge"
}
```

## Ports

| Service | Port |
|---------|------|
| Frontend | 3000 |
| Backend API | 5001 |
| PostgreSQL | 5432 |
| Redis | 6379 |
| SignalR Hub | 5001/hubs/game |

## Fehlerbehebung

### Datenbank-Verbindung fehlgeschlagen
```bash
# Prüfe ob PostgreSQL läuft
docker ps

# Manuelle Verbindung testen
docker exec -it skillforge-postgres psql -U skillforge -d skillforge

# Migration manuell ausführen
docker exec -i skillforge-postgres psql -U skillforge -d skillforge < migration.sql
```

### Backend baut nicht
```bash
# Dotnet Root setzen
export DOTNET_ROOT=/home/linuxbrew/.linuxbrew/opt/dotnet/libexec

# Neu bauen
cd backend/SkillForge.Api
dotnet build
```
