# AirLock — Advanced Architecture & Component Reference

*For AI-assisted development. This document describes every `.cs` and `.axaml`/`.axaml.cs` file in the AirLock codebase, how they relate, and the architectural principles behind them. Pair with the philosophy docs (`Airlock_Workflow_Engine_Architecture.md`, `ArchitectureNotes.md`, `AirlockChatStarter.md`) for the "why"; this document is the "what" and "how."*

---

## 1. System Overview

AirLock is a single-purpose appliance: an Avalonia (.NET 8) desktop application running on Ubuntu 24 LTS, backed by a native PostgreSQL database, a Dockerized Ollama inference runtime (Mistral 7B), and the local filesystem for letter storage and `.airpack` workflow definitions.

```
┌─────────────────────────────────────────────┐
│              Avalonia Desktop App             │
│  (AIB6 namespace — MainWindow + tabs/dialogs) │
└───────────────┬───────────────┬──────────────┘
                │               │
        ┌───────▼──────┐  ┌─────▼──────────┐
        │ Native        │  │ Dockerized     │
        │ PostgreSQL    │  │ Ollama         │
        │ (metadata)    │  │ (Mistral 7B)   │
        └───────────────┘  │ localhost:11434│
                            └────────────────┘
                │
        ┌───────▼──────────────┐
        │ Local Filesystem      │
        │ ~/Documents/AIBDOCS/  │
        │  - letters (.txt)     │
        │  - config/*.airpack   │
        └───────────────────────┘
```

Docker's only job is the inference runtime. Everything stateful — database, letters, configs — lives natively on disk. This split is deliberate (see `ArchitectureNotes.md`): native services are easier to debug, back up, and reason about; Docker is reserved for the one component that benefits from isolation and rapid iteration (the model runtime).

---

## 2. Entry Point & Application Lifecycle

### `Program.cs`
The application's `Main`. Responsibilities:
- Loads `appsettings.json` via `ConfigurationBuilder` (from the current working directory, `optional: false` — the app will not start without it).
- Binds configuration into the static `AppSettings` model (`Program.AppSettings`), accessible globally without dependency injection.
- Calls `PromptTemplateRegistry.Load(AppSettings.Paths.PromptTemplatesFolder)` **before** Avalonia starts — all `.airpack` files are loaded into memory at boot, not lazily.
- Hands off to `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`.

`Program.AppSettings` is the de facto global configuration object — nearly every other file reads from it directly rather than through DI. This is intentional for an appliance-style app: one process, one config, no need for service lifetimes.

### `App.axaml.cs`
Standard Avalonia `Application` subclass. `OnFrameworkInitializationCompleted` sets `desktop.MainWindow = new MainWindow()`. No custom logic beyond the Avalonia boilerplate — styles/resources are defined in the corresponding `App.axaml` (not reviewed in this pass).

### `AppSettings.cs`
Plain POCO configuration model, deserialized from `appsettings.json`:

- `ConnectionStrings.Postgres` — full Npgsql connection string.
- `ModelSettings` — `DefaultModel` (`"mistral"` or `"mixtral"`), plus `Mistral` and `Mixtral` sub-objects (`ModelName`, `Endpoint`). `LetterTab` reads `DefaultModel` at construction to pick which `ModelOption` to use.
- `Paths` — all filesystem locations: `ExportFolder`, `ArchiveFolder`, `PromptTemplatesFolder`, `PromptTemplatesFile`, `ImportFolder`, `ExportUSB`. The last two (`ImportFolder`, `ExportUSB`) are legacy hardcoded USB paths slated for removal once `UsbDriveScanner` is fully wired in.
- `LLM.UseStreaming` — bool flag (currently the streaming path in `LetterTab.CallLlmAsync` is hardcoded to `stream = true` regardless of this setting — worth reconciling).

---

## 3. Main Window & Navigation

### `MainWindow.axaml.cs`
The app shell. On construction:
- Centers the window (`WindowStartupLocation.CenterScreen`).
- Finds the `TabControl` named `MainTabs` and wires `SelectionChanged` → `OnTabChanged`.
- Reads `PromptTemplateRegistry.GetAllTemplates()` and sets the window title to the first loaded template's `Title` field (e.g., "Legal Letters") — meaning **the window title is driven by whichever `.airpack` loads first**, not a fixed app name.

`OnTabChanged` checks if the newly selected tab's header is `"Review Drafts"` and, if so, calls `ArchiveGridView.RefreshGridAsync()` — this is how the archive grid stays current without polling.

The `.axaml` (not reviewed) defines the `TabControl` with at least three `TabItem`s: "Create Drafts" (`LetterTab`), "Review Drafts" (`ArchiveGridView`), and "Import Airpacks" (`ImportTab`, header recently renamed from "Import Templates").

---

## 4. The Drafting Workflow — `LetterTab.axaml.cs`

This is the largest and most central file — the "Create Drafts" tab where the core workflow loop happens.

### Construction
- Disables `SaveButton`, enables `GenerateButton`.
- Picks the active model (`_selectedModel`, `_apiUrl`) based on `AppSettings.ModelSettings.DefaultModel` — `"mixtral"` routes to `ModelSettings.Mixtral`, anything else (including `"mistral"`) routes to `ModelSettings.Mistral`.
- Populates `LetterTypeDropdown` from `PromptTemplateRegistry.GetMainTypeDisplayNames()` — strings of the form `"{Title} > {MainType}"` (e.g., `"Legal Letters > Demand"`).
- Populates `ToneDropdown` with a fixed 5-item escalation ladder: Initial Inquiry → Reminder → Demand → Final Notice → Intent to Escalate.
- Populates `LengthDropdown` with 5 fixed length labels (Brief/Short/Medium/Extended/Full), each annotated with an approximate word count.
- Wires `LetterTypeDropdown.SelectionChanged → OnLetterTypeChanged` and `SubtypeDropdown.SelectionChanged → OnSubTypeChanged`.
- A large block of commented-out code shows a previously wired `VoiceRecorder` (transcript-ready and clear-pressed handlers) — currently disabled, not deleted. Voice input is otherwise handled by `VoiceRecorderControl` as a more self-contained component (see §7).

### Cascading dropdown logic
- **`OnLetterTypeChanged`** — parses the selected `"{Title} > {MainType}"` string, fetches matching sub-types via `PromptTemplateRegistry.GetSubTypesForTitleAndMainType`, populates `SubtypeDropdown`, and sets the `UserInput` watermark to the first sub-type's `InputScaffold` (the `[Who]/[What]/[When]` placeholder text).
- **`OnSubTypeChanged`** — re-resolves the template for the newly selected sub-type and updates the watermark accordingly.

### Prompt Builder integration
**`OnPromptBuilderClick`** opens `PromptBuilderDialog` modally. If the user fills it in and clicks Insert, the dialog's `AdditionalInfo` (already sanitized by the dialog itself) is appended to `UserInput.Text` — either replacing empty input or appended with a blank-line separator.

### Generation pipeline — `OnGenerateClick`
1. Resolves `mainType` (from the dropdown, stripping the `"Title > "` prefix), `subTypeLabel`, `toneLabel`, and `lengthLabel` (stripping the `"(~N words)"` suffix).
2. Maps `toneLabel`/`lengthLabel` through `PromptMappings.MapTone`/`MapLength` to get the actual directive text sent to the model.
3. Resolves the full `PromptTemplate` via `PromptTemplateRegistry.GetSubTypesForMainType` → `GetTemplate`.
4. Calls `template.FillPrompt(userInput, tone, length, mainType, subTypeId)` — this is where `PromptSanitizer.Clean()` is applied to every interpolated field (see §5).
5. Disables `GenerateButton`/`SaveButton`, sets `PreviewBox` to "Generating draft...".
6. Starts a `Stopwatch` and a **fire-and-forget UI timer** (`Task.Run` loop with `Dispatcher.UIThread.InvokeAsync`) that updates `StatusText` with elapsed `MM:SS` every second — this is the live "Generating draft... (00:23)" indicator.
7. Calls `CallLlmAsync(prompt)` on a background thread via `Task.Run`.
8. On completion: stops the stopwatch, sets `cancel = true` to kill the timer loop, writes the result to `PreviewBox`, shows final elapsed time, sets `_letterGenerated = true`, and enables `SaveButton`.

### `CallLlmAsync`
- Logs the full prompt to console (`[DEBUG PROMPT]`).
- POSTs to `_apiUrl` (Ollama's `/api/generate`) with `{ model, prompt, stream: true }`.
- Reads the response as a **streamed** sequence of newline-delimited JSON objects (`HttpCompletionOption.ResponseHeadersRead` + `StreamReader.ReadLineAsync`), accumulating each `"response"` field into a `StringBuilder`.
- Catches per-line JSON parse errors individually (logs `[PARSE ERROR]`, continues) and catches top-level HTTP/connection errors, returning `"[Error calling language model: ...]"` as the result string (which then gets displayed directly in `PreviewBox` — i.e., errors are surfaced to the user as the "draft").
- A static `HttpClient` with a 10-minute timeout is shared across calls (correct practice — avoids socket exhaustion).

### Save pipeline — `OnSaveClick`
1. Builds a filename via `{Type}_{SubType}_{Intent}_{Length}_{Timestamp}.txt`, where each segment is run through `CleanLabel` (PascalCase, spaces removed) except length, which goes through `CleanLengthLabel` (collapses to Short/Medium/Long).
2. Guards: if `_letterGenerated` is false, shows "Please generate a letter first." for 3 seconds and returns.
3. Resolves `Paths.ExportFolder` (expanding `~`), creates the directory if needed, writes `PreviewBox.Text` to `{ExportFolder}/{filename}`.
4. Calls `PostgresHelper.InsertLetterAsync(filename, letterType, DateTime.Now, false, false)` — `letterType` here is the **raw** `"{Title} > {MainType}"` string from the dropdown, not the cleaned `type` used in the filename (worth noting as a minor inconsistency — the DB and filename encode the type differently).
5. Brief "Saving Draft...." status, then "Draft Saved Successfully. Ready For Next Draft." in `PreviewBox`, clears status, re-enables `GenerateButton`.

### Helper methods
- **`Slugify`** — lowercases and replaces non-alphanumeric runs with underscores. Defined but not called anywhere in the visible code (dead code or reserved for future use, e.g. exported filenames or `.airpack` IDs).
- **`GetLengthCode`** — maps a length label to `"short"`/`"med"`/`"long"`. Also currently unused in the visible flow (the actual save path uses `CleanLengthLabel` instead, which returns `"Short"/"Medium"/"Long"` — likely an earlier or parallel naming scheme).

---

## 5. Prompt Construction & Sanitization

### `PromptTemplateRegistry.cs`
Static registry, the runtime representation of all loaded `.airpack` packs.

- **`Load(folderPath)`** — expands `~`, throws `DirectoryNotFoundException` if the folder doesn't exist, then scans for `*.airpack` files (top-directory only, non-recursive). Each file is expected to contain a JSON **array** of `PromptTemplate` objects (`JsonSerializer.Deserialize<List<PromptTemplate>>`, case-insensitive property matching). Files that fail to parse or contain zero templates are skipped with a console log, not a hard error — a single bad `.airpack` doesn't crash startup.
- **`GetAllTemplates()`** — raw accessor, used by `MainWindow` to derive the window title.
- **`GetMainTypeDisplayNames()`** — returns distinct `"{Title} > {MainType}"` strings for the top-level dropdown.
- **`GetSubTypesForTitleAndMainType(title, mainType)`** / **`GetSubTypesForMainType(mainType)`** — return `SubTypeInfo` (Id + Label) lists. Note the asymmetry: the title-scoped version is used on initial dropdown population (`OnLetterTypeChanged`), while the non-title-scoped version is used during generation (`OnGenerateClick`, `OnSubTypeChanged`) — if two packs ever share a `MainType` under different `Title`s, this could resolve to the wrong template. Currently safe because only one pack ("Legal Letters") is loaded.
- **`GetTemplate(mainType, subType)`** — first match by `MainType` + `SubType`.

### `PromptTemplate` class (nested in the same file)
Maps 1:1 to the `.airpack` JSON schema: `Title`, `MainType`, `SubType`, `Label`, `Structure`, `Intent`, `InputScaffold`, `LengthOptions` (list, currently unused by `LetterTab` which has its own hardcoded length list), `ToneDirectives` (dictionary, also currently superseded by `PromptMappings.ToneMap` for the actual directive text — the `.airpack`'s own `ToneDirectives` appears to be legacy/unused in the current flow), `PromptTemplateText`, `RoleInstruction`.

**`FillPrompt(userInput, toneLabel, length, mainType, subType)`** is the prompt assembly function:
- Falls back to a default `[Who]/[What]/[When]/[Where]/[Why]` scaffold if `userInput` is empty.
- Falls back to a generic role instruction if `RoleInstruction` is empty.
- Performs `{Placeholder}` → value substitution on `PromptTemplateText` for: `{UserInput}`, `{Tone}`, `{Length}`, `{Structure}`, `{Intent}`, `{MainType}`, `{SubType}`, `{Role}` — **every** substituted value passes through `PromptSanitizer.Clean()`.
- Appends a fixed anti-hallucination instruction: "Use only the provided facts... If any information is missing, leave a clear placeholder in brackets... Do not invent or assume."

### `PromptSanitizer.cs`
The last line of defense before user text reaches the model.

- `MaxLength = 1000` chars (~250 tokens) — truncates with `…` if exceeded. Flagged in tech debt as possibly too restrictive for future packs accepting richer input (pasted documents, incident reports).
- Normalizes smart quotes/dashes/non-breaking spaces to plain ASCII equivalents.
- Normalizes `\r\n`/`\r` → `\n`.
- Strips backslashes and forward slashes entirely (`.Replace("\\", "").Replace("/", "")`) — note this means file paths, URLs, or fractions typed by the user are silently mangled.
- Converts double quotes to single quotes.
- **Control character stripping (recently fixed):** now `Regex.Replace(safe, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "")` — strips all C0 control characters **except** `\n` (0x0A) and `\t` (0x09). Previously this stripped `\n` too, flattening structured multi-line input into one line before it reached Mistral. This was the highest-impact fix of the current cycle.
- Returns `"[input missing]"` for null/whitespace input — this string can itself end up embedded in a prompt as a placeholder.

### `PromptMappings.cs`
Two static dictionaries that are the **actual** source of truth for tone and length directives sent to the model (superseding the `.airpack`'s own `ToneDirectives`/`LengthOptions` for the current pack):

- `ToneMap` — 5 entries matching `LetterTab`'s `ToneDropdown` items, each mapping to a full sentence of tone guidance.
- `LengthMap` — 5 entries matching `LengthDropdown`'s base labels (Brief/Short/Medium/Extended/Full), each mapping to a word-count instruction sentence.
- `MapTone`/`MapLength` — dictionary lookup with fallback to the input string itself if no match (defensive, shouldn't normally trigger given the dropdowns are hardcoded to match these keys).

---

## 6. Persistence Layer

### `LetterMetadata.cs`
Plain data model: `Filename`, `LetterType`, `Timestamp`, `IsFavorite`, `IsHidden`. Note this file lacks a namespace declaration (sits in the global namespace) — inconsistent with the rest of the codebase (`AIB6`/`AIB6.Helpers`), though functionally harmless in C#.

### `PostgresHelper.cs`
Single static method: **`InsertLetterAsync(filename, letterType, timestamp, favorite, hidden)`**. Opens a fresh `NpgsqlConnection` per call (connection string from `Program.AppSettings.ConnectionStrings.Postgres`), calls the stored procedure `insert_letter(...)` via `CALL`, and disposes. No connection pooling configuration is visible — Npgsql pools by default at the driver level, so this is likely fine for an appliance with low concurrent write volume.

No corresponding "read" methods are present in the reviewed files — `ArchiveGridView` (not reviewed in detail, see §8) presumably has its own query logic for populating the Review Drafts grid, possibly via a different helper or inline Npgsql calls.

The `draft_archive` table mentioned in the README is "reserved — not yet in use," consistent with there being no code referencing it.

---

## 7. Voice Input — `VoiceRecorderControl.axaml.cs`

A self-contained `UserControl` wrapping Dockerized Whisper for speech-to-text.

### State & events
- `isRecording`, `accumulatedTranscript` (capped at `MaxTranscriptLength = 4000` chars, trimmed from the front if exceeded).
- Public events: `TranscriptReady` (fires with the accumulated transcript after each recording stops) and `ClearPressed`.
- `MaxWaveformBars` constants (30 at class level, 93 redeclared locally in the constructor — the local one shadows and is the one actually used).

### Recording flow (`StartButton_Click`)
**Start recording:**
- Swaps the button icon to a red circle (stop icon, drawn via `Avalonia.Controls.Shapes.Path` with raw geometry data).
- Starts a 1-second `DispatcherTimer` (`micStatusTimer`) updating "Recording... (Ns)".
- Starts a 100ms `DispatcherTimer` (`waveformTimer`) that redraws 100 `Border` bars per tick using a sine-wave animation (`6 + 4*sin((angle+i)*0.2)`, `angle` incrementing each tick) — this is a **fake/decorative waveform**, not derived from actual audio amplitude.
- Launches a `docker run --rm --device /dev/snd -v /tmp/airlock_voice:/audio jrottenberg/ffmpeg:6.0-ubuntu -y -f alsa -i default /audio/temp.wav` process — records raw audio from the default ALSA device into a container-mounted volume.

**Stop recording:**
- Kills the ffmpeg container process, waits for exit, brief 500ms delay.
- Swaps the button icon to a green play/triangle.
- Stops both timers, clears the waveform display.
- Starts a "Transcribing..." animated-dots timer (`transcribingDotsTimer`, 500ms, cycling 0-3 dots) and disables `StartButton`.
- Runs `docker exec airlock-whisper /app/build/bin/whisper-cli -m /models/ggml-base.en.bin -f /audio/temp.wav -otxt -of /audio/output` — this assumes a **separate, already-running** `airlock-whisper` container (unlike the ffmpeg recorder, which is `--rm` and ephemeral per recording).
- On completion, reads `/tmp/airlock_voice/output.txt`, appends to `accumulatedTranscript` (newline-separated, trimmed to `MaxTranscriptLength` from the end), stops the dots timer, sets status to "Transcription complete", re-enables `StartButton`, and fires `TranscriptReady`.

### Dead/commented code
Several `TranscribedTextBox` and `CharCountLabel` references are commented out throughout — this control appears to have previously had its own visible transcript textbox and character counter, since removed in favor of the parent (`LetterTab`) consuming `TranscriptReady` directly. The constructor's `dotTimer` field is initialized but appears to duplicate `transcribingDotsTimer`'s purpose — likely leftover from refactoring.

### Constants worth flagging
- Hardcoded paths: `/tmp/airlock_voice/`, container names `airlock-whisper`, model path `/models/ggml-base.en.bin`. None of these come from `AppSettings` — if the Whisper container or model path changes, this file needs a direct edit.

---

## 8. Tabs & Dialogs

### `ImportTab.axaml.cs` — "Import Airpacks" tab
Builds its UI **entirely in code** (not via XAML markup) into a `StackPanel` (`_mainPanel`) that's injected into the `ImportStack` container defined in the `.axaml`.

**UI elements created:**
- Title `TextBlock` ("Import Airpacks").
- Instructions `TextBlock` ("Import new Airpack workflow files.").
- `_importButton` — orange button, "Click - Import Airpack (.airpack)", triggers `OnImportClick` → `RunImportProcess`.
- `_scanUsbButton` — **temporary debug button**, gray, "Debug: Scan USB Drives", triggers `OnScanUsbClick`. Added this cycle to validate `UsbDriveScanner` from inside the running app; calls `UsbDriveScanner.FindMountedDrives()` and prints each drive's label + path to `_statusText`, or "No USB drives detected" if empty. **Should be removed once real Import/Export wiring is complete.**
- `_statusText` — multi-line status output for both buttons.

**`RunImportProcess` (the real import logic):**
1. Reads `Paths.ImportFolder` (currently hardcoded `/mnt/AIBUSB/import`) and `Paths.PromptTemplatesFile` (expanded, then `Path.GetDirectoryName`'d to get the config directory).
2. If `ImportFolder` doesn't exist → "No import folder detected..." and abort. **This is the hardcoded-path problem `UsbDriveScanner` is meant to solve but doesn't yet** — this method still uses `Directory.Exists` on a fixed path rather than calling the scanner.
3. Scans for `*.airpack` files. None found → "No Airpack files found..." and abort.
4. For each file: parses as JSON, checks `_sigil == "owl_440Hz_approved"` (the pack validation gate). Rejects (wrong/missing sigil), skips (already exists at destination), or copies into the config directory, incrementing `totalImported`.
5. Catches per-file exceptions into an `unreadableFiles` list (filename + first line of exception message).
6. Builds a multi-section status message: imported count (✅), skipped, rejected, unreadable — each as its own paragraph.

**Note:** the file is missing explicit `using System.IO;` and `using System.Collections.Generic;` despite using `Directory`, `Path`, `File`, `List<T>` throughout. This either relies on .NET's implicit global usings (enabled by default in .NET 6+ SDK-style projects, which would cover both namespaces) or was already a latent issue pre-dating this cycle's edits.

### `LetterPreviewDialog.cs` — letter preview window
A `Window` subclass, **built entirely in code** (no `.axaml` — note this is a `.cs`-only class, unlike most other UI files which pair `.axaml`+`.axaml.cs`).

**Layout:** `DockPanel` containing:
- A `ScrollViewer` (docked top) wrapping a `TextBlock` (`_textBlock`) showing the letter text, white background, wrapping enabled.
- A button row (docked bottom): **Export** and **Close**, both styled with the same blue (`#0078D7`) accent.
- A status `TextBlock` (`_statusText`, green bold 20pt) docked bottom, below the buttons.

**Export — current state (the core of tech debt item #4):**
- `ExportToUSBAsync` (the **active** handler, wired to the Export button) is a **pure simulation**: sets status to "Exporting..." (black), `Task.Delay(1750)`, then "Export Complete" (green). **No file is actually copied.**
- `ExportToUSBAsyncOld` (present but **not wired to any button**) contains the real-but-superseded implementation: checks `Paths.ExportUSB` (hardcoded `/mnt/AIBUSB/export`) exists, copies `_sourceFilePath` into it if the destination doesn't already exist, and sets an appropriate status message or a friendly error ("We couldn't save your letter...").

**This is the next major piece of work**: replacing `ExportToUSBAsync`'s body with logic that calls `UsbDriveScanner.FindMountedDrives()`, handles zero/one/multiple-drive cases (the multiple-drive picker is tech debt item #7), writes into a standardized `exports/` subfolder (tech debt item #6), and removes the hardcoded `Paths.ExportUSB` dependency entirely.

### `PromptBuilderDialog.axaml.cs` — structured input dialog
A `Window` subclass (paired with `.axaml` for layout — fields not shown in code-behind). On **Insert** (`OnInsert`):
- Reads five fields (`ToField`, `FromField`, `WhereField`, `WhereWhenField`, `WantField`), each passed through `PromptSanitizer.Clean()`.
- Assembles `AdditionalInfo` as a labeled multi-line string: `From:`, `To:`, `Where:`, `What happened:\n{...}`, `What I want:\n{...}` — only including sections with non-empty values.
- `Close(true)`.

On **Cancel** (`OnCancel`): `Close(false)`.

`LetterTab.OnPromptBuilderClick` awaits this dialog and, if `true` with non-empty `AdditionalInfo`, appends it to `UserInput.Text`. Note `AdditionalInfo` is **already sanitized** by this dialog — it then passes through `PromptSanitizer.Clean()` *again* inside `FillPrompt` when the final prompt is assembled. Double-sanitization is harmless here (idempotent for already-clean text) but worth knowing if sanitizer behavior changes.

---

## 9. USB Infrastructure — `UsbDriveScanner.cs` (new this cycle)

Static utility class, the foundation for tech debt items #4-7 (real Export, dynamic Import, drive picker).

- **`MountRoots`** — `/media`, `/run/media`, `/mnt`, scanned in that order.
- **`FindMountedDrives()`** — for each root that exists, recursively (`SearchOption.AllDirectories`) enumerates subdirectories and filters via `IsLikelyMountPoint`. Returns a deduplicated list of full paths.
- **`IsLikelyMountPoint(path)`** — heuristic: directory exists, is accessible, and contains at least one file-system entry. Wrapped in try/catch to handle permission errors or races (drive unplugged mid-scan) by returning `false`.
- **`GetSingleDriveOrNull()`** — convenience wrapper: returns the single drive if `FindMountedDrives().Count == 1`, else `null`. Intended for the common single-USB case in Export/Import, deferring to a picker dialog when the count is 0 or >1.
- **`GetDriveLabel(mountPath)`** — returns the last path segment (typically the volume label under `udisks2`'s `/media/<user>/<LABEL>` convention) as a human-friendly display string.

**Current integration status:** wired only into `ImportTab`'s temporary debug button. Not yet called from `RunImportProcess` or `LetterPreviewDialog.ExportToUSBAsync` — that wiring is the next session's primary work.

**Dependency:** requires `udisks2`/`udiskie` running on the host so USB drives actually mount under `/media` or `/run/media` (see README setup step 6). Without this, `FindMountedDrives()` correctly returns an empty list — there's nothing wrong with the scanner if a plugged-in drive isn't detected and `udiskie` isn't running.

---

## 10. Cross-Cutting Notes & Inconsistencies Worth Tracking

- **Global config via static `Program.AppSettings`** — convenient for an appliance with one config and one lifetime, but means nothing in `Helpers/` or the tabs is independently testable without the full `Program` static state being initialized first.
- **`.airpack` schema fields with unclear current usage**: `PromptTemplate.LengthOptions` and `PromptTemplate.ToneDirectives` exist in the schema and are deserialized, but `LetterTab` uses its own hardcoded dropdown lists plus `PromptMappings` for the actual tone/length directive text. If a future `.airpack` relies on its own `ToneDirectives`/`LengthOptions` to override the global mappings, that path doesn't currently exist.
- **Two parallel sanitizations** of `PromptBuilderDialog` output (once in the dialog, once in `FillPrompt`) — harmless but redundant.
- **`LetterType` stored in Postgres vs. encoded in filename** use different cleaning rules (raw `"Title > MainType"` string vs. PascalCase `CleanLabel` output) — anyone querying the DB to cross-reference filenames should be aware these don't match character-for-character.
- **`ArchiveGridView.axaml.cs`** (Review Drafts tab) has not yet been reviewed in an AI-assisted session. Based on the README and `MainWindow`'s `RefreshGridAsync()` call, it's responsible for querying letter metadata (likely via Postgres, possibly via a helper not yet seen), rendering a grid with favorite/hidden toggles, and refreshing when the tab is selected. **Action item:** upload this file in a future session for full coverage.
- **Hardcoded infrastructure paths outside `AppSettings`**: `VoiceRecorderControl` hardcodes `/tmp/airlock_voice`, the `airlock-whisper` container name, and the Whisper model path. If these ever need to change per-deployment, they currently require a code edit, not a config change.
- **Legacy USB paths** (`Paths.ImportFolder`, `Paths.ExportUSB`) remain in `AppSettings`/`appsettings.json` and are still the active code path in `ImportTab.RunImportProcess` and `LetterPreviewDialog.ExportToUSBAsyncOld`. These should be removed only after `UsbDriveScanner` is fully wired into both flows (per `airlock_usb_import_export.md`'s recommended build order).

---

## 11. Suggested Next Steps (Tech Debt Cross-Reference)

| Item | Files Involved | Status |
|---|---|---|
| Wire `UsbDriveScanner` into `LetterPreviewDialog.ExportToUSBAsync` | `LetterPreviewDialog.cs`, `UsbDriveScanner.cs` | Not started |
| Wire `UsbDriveScanner` into `ImportTab.RunImportProcess` | `ImportTab.axaml.cs`, `UsbDriveScanner.cs` | Not started |
| Standardize USB folder structure (`airpacks/`, `exports/`) | Both above | Not started |
| Drive picker dialog (multi-drive case) | New file (e.g. `UsbDrivePickerDialog.axaml.cs`) | Not started |
| Remove temporary debug scan button | `ImportTab.axaml.cs` | Pending above |
| Remove `Paths.ImportFolder`/`Paths.ExportUSB` from `AppSettings`/`appsettings.json` | `AppSettings.cs`, `appsettings.json` | Pending above |
| Review `ArchiveGridView.axaml.cs` | New upload needed | Not started |
| Reconcile `LengthOptions`/`ToneDirectives` schema fields vs. `PromptMappings` | `PromptTemplateRegistry.cs`, `PromptMappings.cs` | Not started — design decision needed |

---

*Last updated: June 14, 2026 — reflects `.airpack` rename, `PromptSanitizer` newline fix, and `UsbDriveScanner` introduction.*
