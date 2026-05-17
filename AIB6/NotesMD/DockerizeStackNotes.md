````md
# Airlock Full Dockerized Stack Architecture

## Philosophy

Airlock is designed as a sealed, reproducible AI appliance.

Core principles:

- Offline-first
- Rebuildable
- Portable
- Containerized
- Minimal host OS pollution
- Golden-image deployable

The host Linux system should primarily provide:

- Docker
- Docker Compose
- .NET runtime / SDK
- GPU drivers (optional)
- File system storage

All major services should run as isolated containers.

---

# Target Architecture

```text
Avalonia UI (.NET)
        │
        ▼
Docker Service Layer
├── PostgreSQL
├── Ollama (LLM)
├── Whisper (Speech-to-Text)
└── Piper (Text-to-Speech)
````

---

# Host OS Responsibilities

Recommended OS:

```text
Debian 12 Minimal
```

Host installs:

```bash
sudo apt update

sudo apt install -y \
docker.io \
docker-compose \
dotnet-sdk-8.0 \
git \
curl \
ffmpeg
```

Host should remain as clean as possible.

Avoid installing:

* Native PostgreSQL
* Native Python TTS stacks
* Native Whisper installs
* Large ML dependency trees

Those belong inside containers.

---

# Docker Service Topology

## PostgreSQL

Purpose:

* metadata
* archive
* favorites
* logs
* document indexing

Container:

```yaml
postgres:
  image: postgres:15
  container_name: airlock-postgres
  restart: unless-stopped
  environment:
    POSTGRES_USER: ${POSTGRES_USER}
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    POSTGRES_DB: ${POSTGRES_DB}
  ports:
    - "5432:5432"
  volumes:
    - ./db:/var/lib/postgresql/data
```

Connection:

```text
Host=localhost
Port=5432
Database=airlock
Username=airlock
Password=<from .env>
```

---

## Ollama (LLM)

Purpose:

* local inference
* generation
* prompt execution

Container:

```yaml
ollama:
  image: ollama/ollama
  container_name: airlock-llm
  restart: unless-stopped
  ports:
    - "11434:11434"
  volumes:
    - ./models:/root/.ollama
```

Endpoints:

```text
http://localhost:11434/api/generate
```

Models:

```text
mistral
mixtral
llama3
codellama
```

---

## Whisper (Speech-to-Text)

Purpose:

* voice dictation
* transcript generation
* audio import processing

Container Example:

```yaml
whisper:
  image: rhasspy/wyoming-whisper
  container_name: airlock-whisper
  restart: unless-stopped
  ports:
    - "10300:10300"
```

Responsibilities:

* receive audio
* transcribe locally
* return text to Avalonia app

Future enhancements:

* GPU acceleration
* batch processing
* microphone streaming
* speaker diarization

---

## Piper (Text-to-Speech)

Purpose:

* read drafts aloud
* accessibility
* spoken previews

Container Example:

```yaml
tts:
  image: lscr.io/linuxserver/piper:latest
  container_name: airlock-tts
  restart: unless-stopped
  ports:
    - "10200:10200"
  volumes:
    - ./tts:/config
```

Responsibilities:

* receive text
* generate WAV audio
* playback or stream to UI

Preferred voice strategy:

```text
American English
Professional tone
Fast inference
Offline operation
```

Reason Piper is preferred:

* lightweight
* fast startup
* low dependency complexity
* appliance-friendly
* easier offline deployment than Coqui

---

# Unified Service Map

```text
localhost:5432   → PostgreSQL
localhost:11434  → Ollama
localhost:10300  → Whisper
localhost:10200  → Piper TTS
```

---

# Recommended Folder Layout

```text
/opt/airlock

/opt/airlock/app
/opt/airlock/db
/opt/airlock/models
/opt/airlock/tts
/opt/airlock/import
/opt/airlock/export
/opt/airlock/logs
```

---

# Environment Variables

Use `.env` instead of hardcoded credentials.

Example:

```env
POSTGRES_USER=airlock
POSTGRES_PASSWORD=change_this
POSTGRES_DB=airlock
```

Advantages:

* centralized secrets
* easier migration
* safer configuration management
* cleaner compose files

---

# Startup Model

Single command:

```bash
docker compose up -d
```

System recovery becomes:

```bash
git pull
docker compose up -d
```

---

# Backup Strategy

Critical persistent folders:

```text
/opt/airlock/db
/opt/airlock/models
/opt/airlock/templates
/opt/airlock/export
```

Simple backup methods:

```bash
tar -czf airlock_backup.tar.gz /opt/airlock
```

or snapshot entire VM/image.

---

# Architectural Goal

Airlock is not intended to behave like:

```text
Traditional Linux workstation
```

Airlock is intended to behave like:

```text
Offline AI Appliance
```

Meaning:

* reproducible
* portable
* sealed
* service-oriented
* rebuildable in hours
* minimal host dependencies

```
```
