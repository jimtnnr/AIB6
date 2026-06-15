# AirLock Changelog — June 15, 2026

## Session Summary

Continued from June 14's `.airpack` rename and `PromptSanitizer` fix. Today's focus: USB Import/Export real implementation (tech debt items #4-7), model speed/quality exploration, and identifying a plumbing bug in the newest `.airpack` pack.

---

## USB Import/Export — Items #4-7 (Code Complete, Build-Verified)

### #5 — UsbDriveScanner (carried over from 6/14, now fully integrated)

New file `Helpers/UsbDriveScanner.cs`. Static utility scanning `/media`, `/run/media`, `/mnt` for mounted, non-empty directories. Provides `FindMountedDrives()`, `GetSingleDriveOrNull()`, `GetDriveLabel(path)`.

### #4 — Real Export (was simulated)

`LetterPreviewDialog.cs` — `ExportToUSBAsync()` rewritten:
- Calls `UsbDriveScanner.FindMountedDrives()`
- Zero drives → red "No USB drive found. Please insert a USB drive and try again."
- Creates `<drive>/exports/` if missing (`Directory.CreateDirectory`, idempotent)
- Copies the letter file there with `overwrite: true`
- Green success message showing real destination path
- Removed `ExportToUSBAsyncOld` entirely — its logic absorbed into the new implementation
- `Program.AppSettings.Paths.ExportUSB` no longer referenced by this file

### #6 — Standardized USB folder structure + Import rewrite

`ImportTab.axaml.cs` — `RunImportProcess()` rewritten:
- Replaced hardcoded `Paths.ImportFolder` (`/mnt/AIBUSB/import`) with `UsbDriveScanner.FindMountedDrives()`
- Scans `<drive>/airpacks/` for `*.airpack` files (creates folder if missing)
- Same zero-drive error handling as Export
- Status messages now reference drive label and `airpacks/` structure
- `Program.AppSettings.Paths.ImportFolder` no longer referenced by this file

Standard USB structure going forward:
```
USB_DRIVE/
├── airpacks/   ← Import scans here
└── exports/    ← Export writes here
```
Both folders auto-created on first use — any blank USB works.

### #7 — Drive picker dialog (multi-drive case)

Two new files:
- **`UsbDrivePickerDialog.axaml`** — minimal `Window` shell, one named `StackPanel` (`RootStack`)
- **`UsbDrivePickerDialog.axaml.cs`** — builds UI dynamically via `Populate(List<string> drivePaths)`. One `RadioButton` per drive (shared `GroupName`, first pre-checked, `Tag` = mount path, display = `"{label} ({path})"`). Cancel/Select buttons. `ShowDialog<string?>` returns selected path or `null`.

**Decision tree (both Export and Import):**
- 0 drives → error, no picker
- 1 drive → use directly, no picker
- 2+ drives → show picker; Cancel → "Export cancelled."/"Import cancelled." (no error styling); Select → use chosen drive

**Wiring:**
- `LetterPreviewDialog.ExportToUSBAsync` — `LetterPreviewDialog` is itself a `Window`, so `picker.ShowDialog<string?>(this)` works directly
- `ImportTab.RunImportProcess` — `ImportTab` is a `UserControl`, so uses `this.GetVisualRoot() as Window` (same pattern as `LetterTab.OnPromptBuilderClick`). Required converting `RunImportProcess` from `void` to `async Task`, and `OnImportClick` to `async void`.

### Airpack registry reload after import

`RunImportProcess` now calls `PromptTemplateRegistry.Load(...)` again if `totalImported > 0`, keeping the in-memory registry consistent with disk. Status message adds: "Restart AirLock to use the newly imported pack(s)." — `LetterTab`'s dropdowns are still populated once at construction, so a restart is needed for new packs to appear in the UI, but the registry itself and disk state are now consistent immediately after import.

### Debug button — kept intentionally

The "Debug: Scan USB Drives" button added 6/14 to `ImportTab` is being **kept** for ongoing testing (decision made explicitly this session, comment updated from "temporary" to "intentional, ongoing").

### Build status

All of the above (#4-7) **builds and runs without error**. Confirmed via screenshot: Export correctly shows "No USB drive found..." in red when no drive is present (zero-drive error path verified). Import not yet exercised with a real drive. Picker UI not yet exercised with real multi-drive hardware. No physical USB drive was available during this session — see "Outstanding" section below.

### Documentation produced

- **`USB_Import_Export_Reference.md`** — full reference: why Linux USB mounting is hard, `udisks2`/`udiskie` one-time setup + verification/troubleshooting commands, line-by-line walkthrough of `ExportToUSBAsync` and `RunImportProcess`, `_sigil` validation gate explanation, full end-to-end test checklist, known-gaps table.
- **`USB_Drive_Picker_Reference.md`** — deep dive on `UsbDrivePickerDialog`: decision tree, `Populate()` mechanics, `ShowDialog<string?>` return pattern, Export vs Import wiring differences (Window vs UserControl owner resolution), side-by-side comparison table, multi-drive test procedure, design rationale (radio buttons vs dropdown, pre-selected default, shared dialog).

---

## Model Exploration — Mistral vs Phi-3 Mini vs Qwen2.5 3B

### Infrastructure

Three Ollama containers now running side-by-side (confirmed via Portainer):

| Container | Port | Model |
|---|---|---|
| `airlock-ollama` | 11434 | `mistral:7b-instruct-q4_K_M` |
| `airlock-ollama-phi3` | 11435 | `phi3:mini` |
| `airlock-ollama-qwen` | 11436 | `qwen2.5:3b` |

### Benchmark results (curl, `num_ctx: 1024`, prompt: "Write a detailed and thorough formal business letter of around 1000 words...")

| Model | Tokens generated | Time | Tokens/sec | `done_reason` |
|---|---|---|---|---|
| Mistral 7B Q4 | 566 | 137.07s | ~4.1 | `stop` (under-delivered length) |
| Phi-3 Mini | 1438 | 133.83s | ~10.7 | `stop` |
| Qwen2.5 3B | 1093 | 66.61s | **~16.4** | `stop` (close to target length) |

Context window (1024 vs 2048 vs 4096) confirmed **not** to be the bottleneck — generation speed is compute/memory-bandwidth bound per `airlock_hardware_spec.md`, not context-size bound. `num_ctx: 1024` did not truncate the 566-token Mistral output (`done_reason: "stop"`, not `"length"`). **Context window benchmarking (techdebt2.md #2) — resolved: 2048 confirmed correct, no further action needed.**

### In-app test (Qwen, via repurposed `Mixtral` config slot)

Real `.airpack`-driven generation through the actual UI (Legal Strict Facts > Demand > Payment Demand, Intent to Escalate, Brief):

- **12 seconds**, vs. ~2+ minutes typical for Mistral at similar settings
- Output was structurally correct (proper demand-letter shape, appropriate escalation tone, correct placeholder usage for missing amount/date)
- **Quality issues observed:**
  - Markdown asterisks leaked into plain-text output (`**Payment Demand Letter**`, `**To:**`, `**Subject:**`)
  - `[From: Joe Jones, Attorney At Law]` and `Where: Chicago` echoed as raw labeled fields rather than integrated into a proper letterhead/signature block

### Config-switching attempt — rolled back

Attempted to generalize `AppSettings.ModelSettings` from fixed `Mistral`/`Mixtral` properties to a `Dictionary<string, ModelOption> Models`, with corresponding `LetterTab.axaml.cs` lookup logic and a 3-model `appsettings.json`. **This did not build** (specific error not captured before rollback). **Rolled back to original working `AppSettings.cs`/`appsettings.json`** — confirmed these match the pre-session baseline exactly.

**Working alternative used instead:** repurposed the existing (proven-working) `Mixtral` slot — changed its `ModelName` to `qwen2.5:3b` and `Endpoint` to `http://localhost:11436/api/generate`, set `DefaultModel: "mixtral"`. This is the config used for the in-app Qwen test above. Confirms the **existing two-slot `Mistral`/`Mixtral` pattern works** and is the safe foundation for any future multi-model work.

### Hardware extrapolation (Qwen2.5 3B on target appliance hardware)

Based on `airlock_hardware_spec.md`'s ~3x scaling factor (dev machine → Beelink SER8, due to iGPU offload + DDR5 dual-channel), applied to Qwen's measured ~16.4 tok/sec on the dev machine:

| | Dev (5700U), measured | SER8, extrapolated (~3x) |
|---|---|---|
| Mistral 7B Q4 | ~5-6 tok/sec | 15-20 tok/sec |
| Qwen2.5 3B | ~16.4 tok/sec | ~45-55 tok/sec |

Translated to letter generation time on SER8:

| Length | Mistral 7B | Qwen2.5 3B (extrapolated) |
|---|---|---|
| 500 words | ~35-45 sec | ~12-15 sec |
| 1000 words | ~70-90 sec | ~24-30 sec |

This is extrapolation from a single benchmark on one prompt, not a validated measurement — but the magnitude (roughly 3x faster than Mistral on the same hardware) is large enough to be worth taking seriously as a direction for further testing.

---

## Bug Identified — `.airpack` Header/Footer Plumbing Gap

Reviewed newly-uploaded `legal_strict_facts_header_footer.airpack` (3 templates: Insurance Claim Appeal — Strict Facts, Insurance Claim Appeal, Payment Demand — all under new "Legal Strict Facts" title).

**Problem:** This pack's `PromptTemplateText` references `{HeaderTemplate}`, `{RecipientTemplate}`, and `{FooterTemplate}` placeholders, and defines corresponding top-level JSON fields (`HeaderTemplate`, `RecipientTemplate`, `FooterTemplate` — each containing sub-placeholders like `{SenderName}`, `{Date}`, etc.).

**However**, the current `PromptTemplate` C# class (`PromptTemplateRegistry.cs`) has no properties for these three fields, and `FillPrompt()`'s `.Replace()` chain only covers `{UserInput}`, `{Tone}`, `{Length}`, `{Structure}`, `{Intent}`, `{MainType}`, `{SubType}`, `{Role}`.

**Result:** `{HeaderTemplate}`, `{RecipientTemplate}`, `{FooterTemplate}` are sent to the model as **literal unresolved text** — the model receives these as literal strings in the prompt and has to improvise what to do with them. This likely contributes to (though may not be the sole cause of) the letterhead/signature-block formatting issues observed in the Qwen test above (e.g., `[From: Joe Jones, Attorney At Law]` appearing as a raw bracketed field rather than a formatted sender block).

**Status:** Identified, not yet fixed. This affects **all models equally** — it's a plumbing bug independent of the Mistral/Qwen question. Agreed as the next priority before further model comparison, since a fair model comparison needs both models working from a correctly-substituted prompt.

---

## Decisions Made

1. **No model selector tab for end users, ever** — model choice is a deployment/configuration decision, not a user-facing feature, consistent with AirLock's "factorial collapse" / appliance philosophy. Confirmed analogy: end users of Claude/ChatGPT don't pick models; the product picks for them.

2. **However — a "Models" tab with radio-button selection across ~5 models IS planned**, reframed as: the runtime stays fixed, but the *inference layer* should be swappable the same way `.airpack` packs make the *workflow layer* swappable. The local-LLM landscape moves fast; hardcoding one model forever makes every future model upgrade a code change. Default ships pre-selected; a typical user never needs to open the tab.
   - **Build approach for this**: extend the existing proven `Mistral`/`Mixtral`-style named-slot pattern in `AppSettings.cs` (additive — more named `ModelOption` slots + more cases in `LetterTab`'s selection logic) rather than the `Dictionary<string, ModelOption>` redesign that failed to build today. Build and verify each incremental step before adding UI on top.
   - **Open design question for next session**: does selecting a model in the tab write to `appsettings.json` + require restart (matches today's proven manual workflow), or take effect without restart (requires `LetterTab` to re-read model config at generate-time, not just construction)?

3. **Created `USB_Drive_Picker_Reference.md` and updated `USB_Import_Export_Reference.md`** gap table to reflect #7 completion and the registry-reload fix.

---

## New/Modified Files (This Session)

**New:**
- `Helpers/UsbDriveScanner.cs`
- `UsbDrivePickerDialog.axaml`
- `UsbDrivePickerDialog.axaml.cs`
- `NotesMD/.../USB_Import_Export_Reference.md`
- `NotesMD/.../USB_Drive_Picker_Reference.md`
- `NotesMD/.../Model_Comparison_Test.md` (template created, not yet fully filled in)
- `NotesMD/.../Switching_Models_Dev_Guide.md` (written against the failed dictionary design — **needs revision** once the named-slot approach is implemented, since the dictionary syntax it documents doesn't match the working code)

**Modified (build-verified, in place):**
- `LetterPreviewDialog.cs` — real Export, picker integration, `ExportToUSBAsyncOld` removed
- `ImportTab.axaml.cs` — real Import via `UsbDriveScanner`, picker integration, async conversion, registry reload on import, debug button retained

**Modified then rolled back (not in place — baseline restored):**
- `AppSettings.cs` — dictionary-based `ModelSettings.Models` attempt, did not build, reverted to original `Mistral`/`Mixtral` properties
- `LetterTab.axaml.cs` — corresponding dictionary lookup logic, reverted
- `appsettings.json` — 3-model dictionary config, reverted to original (Mistral/Mixtral, includes `ImportFolder`/`ExportUSB` which are now dead code but still present)

**Currently live config change (manual, not committed to a "clean" state):**
- `appsettings.json` — `Mixtral` slot repurposed: `ModelName: "qwen2.5:3b"`, `Endpoint: "http://localhost:11436/api/generate"`, `DefaultModel: "mixtral"`. This is a working test configuration, not the intended long-term setup — worth deciding whether to keep, revert to Mistral default, or formalize as part of the future Models tab work.

---

## Outstanding / Carried Forward

1. **No physical USB drive tested this session.** #4-7 are code-complete and build/run without error, but the actual file-copy happy paths (Export writing to a real drive, Import reading `.airpack` files from a real drive's `airpacks/` folder, picker dialog with 2+ real drives) remain unverified. `udisks2`/`udiskie` setup also not yet confirmed on this machine.

2. **`{HeaderTemplate}`/`{RecipientTemplate}`/`{FooterTemplate}` plumbing fix** — next priority. Requires adding three properties to `PromptTemplate` class and corresponding `.Replace()` calls in `FillPrompt()`. Small, contained, affects all models equally.

3. **Models tab (5-model radio selector)** — planned, design approach agreed (extend named-slot pattern, not dictionary), open question on restart-vs-live-switch remains.

4. **`appsettings.json` currently has Qwen in the `Mixtral` slot** — decide whether to keep for continued testing or revert to Mistral default before next session.

5. **`Switching_Models_Dev_Guide.md` documents the failed dictionary approach** — needs rewriting once the named-slot extension is built, so the doc matches actual working code.

6. **Re-run model comparison (`Model_Comparison_Test.md`) on a corrected prompt** — once the header/footer plumbing fix lands, re-test Mistral vs Qwen (vs Phi-3) on the same `.airpack` with working substitution, for a fair quality comparison.

7. **Remaining cleanup from 6/14**: `Paths.ImportFolder`/`Paths.ExportUSB` are dead code in `AppSettings.cs`/`appsettings.json` (no longer referenced by `LetterPreviewDialog.cs` or `ImportTab.axaml.cs`) but still present in the restored baseline — removal deferred pending the Models tab work, since `AppSettings.cs` will be touched again then anyway.

---

*Session date: June 15, 2026*
