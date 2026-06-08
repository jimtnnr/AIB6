# AirLock AI

**Airgapped local AI appliance for professional drafting workflows.**

AirLock is a sealed, offline AI platform that generates professional letters and documents using a local language model. No cloud. No subscriptions. No data leaves the machine.

---

## What It Does

AirLock turns structured user input into polished professional letters in under 60 seconds — entirely offline, on local hardware, with no internet dependency.

The user selects a letter type, fills in the facts, hits Generate. The local AI does the rest.

Built for professionals who handle sensitive correspondence and cannot risk client data leaving their premises.

---

## How It Works

```
User fills in facts
        ↓
AirLock selects the correct .airpack workflow
        ↓
Prompt is assembled from template + facts + tone + length
        ↓
Mistral 7B (local, offline) generates the draft
        ↓
User reviews, saves, exports
```

Everything runs locally. The model runs in Docker. The app runs natively on Linux. Nothing phones home.

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI | C# / Avalonia (cross-platform desktop) |
| Local AI runtime | Ollama in Docker |
| Language model | Mistral 7B (`mistral:7b-instruct-q4_K_M`) |
| Database | PostgreSQL (native, not containerized) |
| Voice input | Whisper (Docker container) |
| OS | Ubuntu 24 LTS |
| Workflow packs | `.airpack` JSON files |

---

## Architecture

```
AirLock Avalonia App
        ↓
localhost:11434 (HTTP API)
        ↓
Ollama Docker Container
        ↓
Mistral 7B (local inference, CPU/iGPU)
```

**Docker** handles the inference runtime — isolated, replaceable, reproducible.

**PostgreSQL** runs natively — stateful data stays simple and debuggable.

**Filesystem** stores the actual letter files — the database is metadata only.

---

## The .airpack System

AirLock is a platform, not a fixed application. The UI is driven entirely by `.airpack` metadata files — JSON workflow packs that define:

- Letter types and sub-types (populate the dropdowns)
- Context scaffolding (guides user input)
- Hidden prompt logic (tone, structure, role, intent)
- Output formatting behavior

Swap the `.airpack` files, swap the entire vertical. The runtime stays fixed. The domain intelligence lives in the packs.

**Current pack:** Legal Letters (demand letters, appeals, notices)

**Pack schema:**
```json
{
  "Title": "Legal Letters",
  "MainType": "Demand",
  "SubType": "payment_demand",
  "Label": "Payment Demand",
  "Structure": "Formal demand letter outlining debt, timeline, and requested action.",
  "Intent": "Demand payment for an outstanding obligation.",
  "InputScaffold": "[Who] owes the money\n[Amount owed]\n[When] payment was due",
  "ToneDirectives": {
    "Reminder": "Use a firm but respectful tone.",
    "Demand": "Use a direct and assertive tone."
  },
  "PromptTemplateText": "...",
  "RoleInstruction": "You are an experienced legal assistant...",
  "_sigil": "owl_440Hz_approved"
}
```

The `_sigil` field is a pack validation mechanism — only verified packs are accepted on import.

---

## Hardware

### Target Appliance Spec
- **Machine:** Beelink SER8 (or equivalent)
- **CPU:** AMD Ryzen 9 8845HS
- **iGPU:** AMD Radeon 780M
- **RAM:** 32GB DDR5 dual-channel
- **Storage:** 1TB NVMe
- **OS:** Ubuntu 24 LTS
- **Inference:** 15-20 tokens/second on Mistral 7B Q4

### Development Machine (current)
- AMD Ryzen 7 5700U
- 38GB RAM
- ~5-6 tokens/second (CPU-only, older iGPU)
- ~60 seconds per 500-word letter

---

## Setup

### Requirements
- Ubuntu 24 LTS
- Docker Engine
- PostgreSQL 16 (native)
- .NET 8 SDK

### 1. Set CPU Governor to Performance

```bash
sudo apt install cpufrequtils -y
sudo cpupower frequency-set -g performance
sudo systemctl enable cpupower
```

This is required. Default `powersave` governor cuts inference speed by more than half.

### 2. Create the Ollama Container

```bash
docker run -d \
  --name airlock-ollama \
  -p 11434:11434 \
  -v ollama:/root/.ollama \
  -e OLLAMA_HOST=0.0.0.0:11434 \
  -e OLLAMA_NUM_THREADS=16 \
  -e OLLAMA_CONTEXT_LENGTH=2048 \
  --restart unless-stopped \
  ollama/ollama
```

### 3. Pull the Model

```bash
docker exec airlock-ollama ollama pull mistral:7b-instruct-q4_K_M
```

### 4. Set Up Boot Warmup

```bash
sudo nano /etc/systemd/system/airlock-warmup.service
```

```ini
[Unit]
Description=Airlock Ollama Model Warmup
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
ExecStartPre=/bin/sleep 10
ExecStart=/usr/bin/docker exec airlock-ollama ollama run mistral:7b-instruct-q4_K_M ""
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable airlock-warmup.service
```

### 5. Set Up PostgreSQL

```bash
sudo -u postgres psql
CREATE USER airlock WITH PASSWORD 'airlock';
CREATE DATABASE airlock OWNER airlock;
\q

psql -U airlock airlock < NotesMD/Postgres/full_dump.sql
```

### 6. Configure appsettings.json

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Username=airlock;Password=airlock;Database=airlock"
  },
  "ModelSettings": {
    "DefaultModel": "mistral",
    "Mistral": {
      "ModelName": "mistral:7b-instruct-q4_K_M",
      "Endpoint": "http://localhost:11434/api/generate"
    }
  },
  "Paths": {
    "ExportFolder": "~/Documents/AIBDOCS",
    "ArchiveFolder": "~/Documents/AIBDOCS",
    "PromptTemplatesFolder": "~/Documents/AIBDOCS/config",
    "PromptTemplatesFile": "~/Documents/AIBDOCS/config/prompt_templates.json",
    "ImportFolder": "/mnt/AIBUSB/import",
    "ExportUSB": "/mnt/AIBUSB/export"
  },
  "LLM": {
    "UseStreaming": true
  }
}
```

### 7. Build and Run

```bash
dotnet build
dotnet run
```

---

## Project Structure

```
AIB6/
├── NotesMD/                          # Architecture and operations docs
│   ├── DescriptionForAIChat/         # Context docs for AI-assisted development
│   ├── Mistral/                      # Ollama/Mistral performance and install docs
│   ├── Portainer/                    # Container management notes
│   ├── Postgres/                     # Database schema and operations
│   │   └── full_dump.sql             # Current schema + data export
│   ├── TechDebt/                     # Known issues and backlog
│   └── Misc/                         # Strategy and business model docs
├── PromptTemplates/                  # .airpack workflow files
├── Helpers/
│   ├── PromptTemplateRegistry.cs     # Loads and serves .airpack files
│   ├── PromptSanitizer.cs            # Cleans user input before sending to model
│   ├── PromptMappings.cs             # Maps tone/length labels to directives
│   └── PostgresHelper.cs            # Database operations
├── Models/
│   └── LetterMetadata.cs             # Letter record model
├── LetterTab.axaml / .cs             # Main drafting UI
├── ArchiveGridView.axaml / .cs       # Review Drafts tab
├── ImportTab.axaml / .cs             # Import Airpacks tab
├── PromptBuilderDialog.axaml / .cs   # Structured input dialog
├── VoiceRecorderControl.axaml / .cs  # Voice input (Whisper)
├── LetterPreviewDialog.cs            # Letter preview and export
├── AppSettings.cs                    # Settings model
├── appsettings.json                  # Runtime configuration
├── Program.cs                        # Entry point
└── README.md                         # This file
```

---

## Key Files

| File | Purpose |
|---|---|
| `appsettings.json` | Model endpoint, paths, database connection |
| `PromptTemplateRegistry.cs` | Loads all `.airpack` files at startup |
| `LetterTab.axaml.cs` | Builds prompt, calls Ollama, streams response |
| `PromptSanitizer.cs` | Sanitizes all input before it reaches the model |
| `full_dump.sql` | Restore point for the PostgreSQL database |

---

## Database

PostgreSQL stores **metadata only** — not letter content.

| Table | Purpose |
|---|---|
| `letters` | Filename, type, timestamp, favorite, hidden flags |
| `draft_archive` | Reserved — not yet in use |

Letters themselves are saved as `.txt` files in `~/Documents/AIBDOCS/`.

Filename convention encodes all metadata:
```
{Type}_{SubType}_{Intent}_{Length}_{Timestamp}.txt
Appeal_InsuranceClaimAppeal_Reminder_Medium_20260516_141533.txt
```

---

## Known Issues / Tech Debt

See `NotesMD/TechDebt/airlock_tech_debt.md` for the full backlog. Key items:

- Export button is currently a simulation — does not copy files to USB
- USB import uses hardcoded path — needs dynamic drive detection
- `.airpack` rename not yet applied throughout codebase (still `.aibcodex` in places)
- `PromptSanitizer` strips newlines from user input — degrades model output quality

---

## Documentation

All architecture, operations, and business docs live in `NotesMD/`:

| Document | Location |
|---|---|
| Ollama install + performance | `NotesMD/Mistral/InstallingMistralOnDocker.md` |
| Ollama philosophy + operations | `NotesMD/Mistral/airlock_ollama_complete.md` |
| Hardware spec + inference numbers | `NotesMD/Mistral/airlock_hardware_spec.md` |
| Airpack platform architecture | `NotesMD/DescriptionForAIChat/airpack_platform_architecture.md` |
| PostgreSQL architecture | `NotesMD/Postgres/airlock_postgres.md` |
| USB import/export solution plan | `NotesMD/TechDebt/airlock_usb_import_export.md` |
| Tech debt backlog | `NotesMD/TechDebt/airlock_tech_debt.md` |
| Coffee Hour next steps | `NotesMD/Misc/airlock_coffee_hour.md` |

---

## Design Philosophy

- **Offline-first.** No cloud dependency. No telemetry. No accounts.
- **Appliance mindset.** The infrastructure disappears. The user sees AirLock.
- **Factorial collapse.** Every visible option removed reduces support complexity.
- **Runtime stays fixed. Market layer stays fluid.** Airpacks handle vertical specialization.
- **Docker for inference. Native for everything stateful.**

> *The runtime should be boring. The product should be sharp.*

---

## Status

**MVP operational.** Currently in discovery and refinement phase.

- Local inference: ✅ Working
- Letter generation: ✅ Working
- Archive/review: ✅ Working
- Voice input: ✅ Working
- USB export: ⚠️ Simulated — real implementation pending
- USB import: ⚠️ Hardcoded path — dynamic detection pending
- Golden image: 🔲 Not yet built

---

*AirLock AI — Built in Arlington Heights, IL*
