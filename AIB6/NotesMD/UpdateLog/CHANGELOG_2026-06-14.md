# AirLock Changelog — June 14, 2026

## Fixed

- **PromptSanitizer.cs** — Control-character stripping regex updated to preserve `\n` (0x0A) and `\t` (0x09). Previously all whitespace control characters were stripped, flattening structured user input (e.g. `[Who]/[What]/[When]` scaffolds) into a single line before reaching Mistral.

- **appsettings.json** — Verified `ModelSettings.Mistral.ModelName` is correctly pinned to `mistral:7b-instruct-q4_K_M` (no change needed; already correct).

## Renamed

- **`.aibcodex` → `.airpack`** (branding alignment, file extension matches product name "Airpack")
  - `PromptTemplateRegistry.cs` — file scan pattern updated to `*.airpack`
  - `ImportTab.axaml.cs`:
    - File scan pattern updated to `*.airpack`
    - Button label: "Click - Import Codex (.aibcodex)" → "Click - Import Airpack (.airpack)"
    - Status message updated to reference `.airpack`
    - Tab title: "Import Codex Packs" → "Import Airpacks"
    - Instructions text: "Import new prompt templates." → "Import new Airpack workflow files."
  - `MainWindow.axaml` — tab header: "Import Templates" → "Import Airpacks"

**Manual step required (done):** existing `.aibcodex` files in `~/Documents/AIBDOCS/config/` renamed to `.airpack` on disk to match the new scan pattern.

## Added

- **`Helpers/UsbDriveScanner.cs`** (new) — Static utility for dynamic USB drive detection. Scans `/media`, `/run/media`, and `/mnt` for mounted, non-empty directories (removable drive heuristic). Provides:
  - `FindMountedDrives()` — returns all detected mount points
  - `GetSingleDriveOrNull()` — convenience for single-drive case
  - `GetDriveLabel(path)` — friendly volume label from mount path
  - Lays groundwork for tech debt items #4–7 (real Export, dynamic Import path, drive picker)

- **Temporary debug button in ImportTab** — "Debug: Scan USB Drives" button calls `UsbDriveScanner.FindMountedDrives()` and prints results to the status text, for in-app testing without a separate console project. **To be removed** once scanner is wired into real Import/Export flows.

## Not Yet Tested

- `UsbDriveScanner` has not been run against a real USB drive yet. Requires `udisks2`/`udiskie` installed and running for drives to auto-mount under `/media` or `/run/media` (see `airlock_usb_import_export.md`). Next session: build, plug in USB, click debug scan button, report results.

## Up Next (Tech Debt #4–7)

1. Validate `UsbDriveScanner` against real hardware
2. Fix Export — replace `Task.Delay` simulation in `LetterPreviewDialog.cs` with real file copy using the scanner
3. Standardize USB folder structure (`airpacks/` + `exports/`) for Import and Export
4. Build drive picker dialog for multi-drive scenarios
5. Remove temporary debug button from ImportTab
