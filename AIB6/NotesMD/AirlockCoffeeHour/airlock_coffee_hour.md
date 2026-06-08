# AirLock Coffee Hour — Next Steps

*One hour. One coffee. Move the project forward.*

---

## What Is Airlock Coffee Hour

A focused one-hour working session to advance AirLock — code, docs, infrastructure, or business. No sprawl. No rabbit holes. Pick a track, execute, document, stop.

---

## Session Log

### Coffee Hour 1 — June 8, 2026 ✅
**Theme: Ollama Performance & Project Documentation**

Accomplished:
- Diagnosed slow inference — CPU governor on `powersave`, GPU container flags
- Fixed CPU governor (`powersave` → `performance`) — 4400MHz unlocked
- Rebuilt Ollama container clean — no NVIDIA flags, 16 threads, 2048 context
- Pulled correct model: `mistral:7b-instruct-q4_K_M`
- Set up boot warmup systemd service
- Established honest performance baseline (~1 min/letter on 5700U)
- Identified target hardware: Beelink SER8 at $599
- Renamed file extension: `.aibcodex` → `.airpack`
- Built 9 reference documents covering infrastructure, business model, hardware, platform architecture

---

## Next Sessions — Prioritized Track List

Pick one track per Coffee Hour. Don't mix tracks.

---

### Track A — Fix the Export Button
**Estimated time: 1 Coffee Hour**
**Priority: CRITICAL — currently non-functional**

The Export button in `LetterPreviewDialog.cs` is a simulation. It waits 1.75 seconds and says "Export Complete" without copying anything.

Tasks:
- Build `UsbDriveScanner` utility class (scan `/media`, `/run/media`, `/mnt`)
- Wire into `ExportToUSBAsync()` — real file copy to USB
- Handle no-drive-found with clear message
- Handle multiple drives with picker dialog
- Create `exports/` folder on USB automatically if missing

Files: `LetterPreviewDialog.cs`, new `UsbDriveScanner.cs`

---

### Track B — Fix the Import Pipeline
**Estimated time: 1 Coffee Hour**
**Dependency: Track A (UsbDriveScanner built first)**

Tasks:
- Replace hardcoded `/mnt/AIBUSB/import` with `UsbDriveScanner`
- Scan `{selectedDrive}/airpacks/` for `*.airpack` files
- Create `airpacks/` folder on USB automatically if missing
- Rename all `.aibcodex` references → `.airpack` throughout codebase
- Update UI labels: "Import Codex" → "Import Airpack"
- Remove hardcoded USB paths from `appsettings.json`

Files: `ImportTab.axaml.cs`, `PromptTemplateRegistry.cs`, `appsettings.json`

---

### Track C — Fix the PromptSanitizer Newline Bug
**Estimated time: 30 minutes — half a Coffee Hour**

The sanitizer strips `\n` characters, flattening all structured user input into one line before it reaches Mistral.

Tasks:
- Fix regex in `PromptSanitizer.cs` to preserve `\n` and `\t`
- Test with a real letter generation — verify structured input improves output quality

Files: `PromptSanitizer.cs`

---

### Track D — Update Model Name in appsettings.json
**Estimated time: 5 minutes — do this first next session**

Tasks:
- Change `"ModelName": "mistral"` → `"ModelName": "mistral:7b-instruct-q4_K_M"`
- Test a real letter generation from Airlock UI
- Verify timer, streaming, and save all work correctly

Files: `appsettings.json`

---

### Track E — Benchmark Context Window
**Estimated time: 30 minutes**

Test whether reducing `OLLAMA_CONTEXT_LENGTH` from 2048 improves tokens/second meaningfully on the 5700U.

Tasks:
- Run benchmark at 2048, 1024
- Compare tok/sec
- Update docker run command in docs if a better value is found
- Update `InstallingMistralOnDocker.md` with findings

---

### Track F — Configure udiskie Automount
**Estimated time: 30 minutes — do before Track A/B**

Install and configure USB automount on the Airlock machine so drives mount reliably without manual intervention.

```bash
sudo apt install udisks2 udiskie -y
```

Tasks:
- Install udiskie
- Configure autostart
- Test with 2 USB drives inserted simultaneously
- Verify mount paths
- Document in `InstallingMistralOnDocker.md`

---

### Track G — Golden Image Planning
**Estimated time: 1 Coffee Hour — planning only**

Define exactly what the Airlock golden image contains so any new machine can be stood up from scratch in under 30 minutes.

Tasks:
- Document every install step from bare Ubuntu to running Airlock
- CPU governor permanent config
- Docker install
- Ollama container + model pull
- udiskie automount
- PostgreSQL native install + schema restore
- Airlock app install + appsettings.json
- Boot warmup service
- Autostart Airlock on login

Output: `airlock_golden_image.md`

---

## Recommended Next 3 Coffee Hours

| Session | Track | Why |
|---|---|---|
| Next | D (5 min) + F (30 min) + C (30 min) | Quick wins, no new files needed |
| After that | A — Export fix | Highest user-facing impact, currently broken |
| After that | B — Import fix | Depends on Track A's UsbDriveScanner |

---

## Running Decisions Log

| Decision | Date |
|---|---|
| Hardware spec: Beelink SER8 at $599 | June 2026 |
| Model: `mistral:7b-instruct-q4_K_M` | June 2026 |
| File extension: `.airpack` (not `.aibcodex`) | June 2026 |
| CPU governor: must be `performance` on every machine | June 2026 |
| Export button is currently a simulation — fix next | June 2026 |
| PostgreSQL native (not in Docker) | Prior session |
| Mixtral removed from active plan | Prior session |
| Legal vertical only for v1 | Prior session |

---

*Started: June 2026 — Airlock Coffee Hour 1*
