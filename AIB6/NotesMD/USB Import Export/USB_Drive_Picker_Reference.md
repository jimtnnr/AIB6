# AirLock — USB Drive Picker Dialog Reference

*How the multi-drive picker works, when it appears, and how it's wired into Export and Import. Companion to `USB_Import_Export_Reference.md` — read that first for the overall Import/Export pipeline; this document goes deep on the picker specifically (tech debt item #7).*

---

## 1. What Problem This Solves

`UsbDriveScanner.FindMountedDrives()` can return 0, 1, or many mounted drives. The first two cases are unambiguous:

- **0 drives** → show an error, nothing to pick
- **1 drive** → use it, no decision needed

**2+ drives** is the ambiguous case. Earlier versions of Export/Import just silently picked `drives[0]` (whatever the filesystem happened to enumerate first — not guaranteed to be insertion order) and mentioned which one was used *after the fact*, in the result message.

The picker dialog replaces that with asking the user *before* anything happens, whenever there's a real choice to make.

---

## 2. When the Picker Appears

```
FindMountedDrives()
        │
        ├── 0 drives ──► red error message, stop. No picker.
        │
        ├── 1 drive ───► use it directly. No picker.
        │
        └── 2+ drives ─► show UsbDrivePickerDialog
                                │
                                ├── user picks one + clicks Select ──► proceed with that drive
                                │
                                └── user clicks Cancel ──► abort gracefully, no error
                                                            ("Export cancelled." / "Import cancelled.")
```

The picker is a **modal dialog** — it blocks interaction with the parent window until the user picks or cancels. Both Export and Import use the identical dialog and identical decision tree; only the post-selection logic (copy direction, folder name) differs.

---

## 3. The Dialog Itself — `UsbDrivePickerDialog`

Two files, both new:

- **`UsbDrivePickerDialog.axaml`** — minimal shell, just a `Window` containing one named `StackPanel` (`RootStack`)
- **`UsbDrivePickerDialog.axaml.cs`** — all actual UI is built in code (same pattern as `ImportTab`), via the `Populate()` method

### Why build the UI in code instead of XAML?

The number of drives isn't known until runtime — could be 2, could be 4. Rather than write XAML that handles a variable number of radio buttons, the dialog exposes one method, `Populate(List<string> drivePaths)`, that builds exactly the right number of `RadioButton` controls at call time. This mirrors how `ImportTab` builds its whole UI in code, for the same reason (dynamic content).

### `Populate(drivePaths)` — what it builds

For a 2-drive example (`/media/jim/AIRLOCK`, `/media/jim/KINGSTON`), the dialog looks like:

```
┌─────────────────────────────────────────┐
│  Multiple USB Drives Detected            │
│  Select which drive to use:              │
│                                          │
│  ●  AIRLOCK   (/media/jim/AIRLOCK)       │
│  ○  KINGSTON  (/media/jim/KINGSTON)      │
│                                          │
│                      [Cancel] [Select]   │
└─────────────────────────────────────────┘
```

Step by step:

1. **Title** — "Multiple USB Drives Detected" (bold, 18px)
2. **Subtitle** — "Select which drive to use:"
3. **One `RadioButton` per drive**, all sharing `GroupName = "UsbDriveChoice"` (this is what makes them mutually exclusive — Avalonia enforces "only one checked" within a shared group name). Each radio button:
   - Displays `"{label}  ({full path})"` — e.g. `"AIRLOCK  (/media/jim/AIRLOCK)"`
   - Stores the **full mount path** in its `Tag` property (the label is just for display; `Tag` is what gets read back)
   - The **first** radio button (`i == 0`) starts pre-checked, so there's always a valid default if the user just clicks Select immediately
4. **Cancel button** — plain, closes the dialog returning `null`
5. **Select button** — blue accent (matches Export/Close button styling elsewhere), finds whichever radio button is currently checked and closes the dialog returning that drive's path

### The return value

`UsbDrivePickerDialog` is a `Window`. Both callers invoke it as:

```csharp
var picker = new UsbDrivePickerDialog();
picker.Populate(drives);
var picked = await picker.ShowDialog<string?>(ownerWindow);
```

`ShowDialog<string?>` is Avalonia's generic modal-dialog pattern — the `<string?>` type parameter says "this dialog will hand back a nullable string when it closes." Internally:

- **Select** → `Close(_selectedDrive)` → `picked` = the chosen mount path (e.g. `"/media/jim/KINGSTON"`)
- **Cancel** → `Close(null)` → `picked` = `null`

The caller's only job is: `if (picked == null) → abort; else → proceed using picked as the drive path`.

---

## 4. How Export Uses It — `LetterPreviewDialog.ExportToUSBAsync`

`LetterPreviewDialog` **is itself a `Window`** (not a `UserControl`), so it can pass `this` directly as the picker's owner:

```csharp
var picker = new UsbDrivePickerDialog();
picker.Populate(drives);
var picked = await picker.ShowDialog<string?>(this);   // 'this' = LetterPreviewDialog

if (picked == null)
{
    _statusText.Text = "Export cancelled.";
    _statusText.Foreground = Brushes.Black;
    return;
}

selectedDrive = picked;
```

After that, the rest of `ExportToUSBAsync` is unchanged from the single-drive path — it creates `<selectedDrive>/exports/`, copies the file, shows the green success message. The picker is purely a *selection* step; everything downstream doesn't know or care whether the drive came from "only one was found" or "user picked it from a list."

---

## 5. How Import Uses It — `ImportTab.RunImportProcess`

This one's slightly more involved because `ImportTab` is a `UserControl`, not a `Window` — `this` can't be passed directly as a dialog owner. The owning `Window` has to be found via the visual tree:

```csharp
var picker = new UsbDrivePickerDialog();
picker.Populate(drives);
var picked = await picker.ShowDialog<string?>(this.GetVisualRoot() as Window);

if (picked == null)
{
    _statusText.Text = "Import cancelled.";
    return;
}

selectedDrive = picked;
```

`this.GetVisualRoot()` walks up from the `ImportTab` control to whatever `Window` contains it (in practice, `MainWindow`). This is the **same pattern** already used elsewhere in the codebase — `LetterTab.OnPromptBuilderClick` does the identical `this.GetVisualRoot() as Window` to open `PromptBuilderDialog`. So this isn't a new pattern, just applied to a new dialog.

### A second consequence: `RunImportProcess` had to become `async`

Before the picker, `RunImportProcess()` was a synchronous `void` method — everything in it (file I/O, JSON parsing) is fast and blocking. `ShowDialog` is inherently asynchronous (it has to wait for the user), so:

- `RunImportProcess()` → `async Task RunImportProcess()`
- `OnImportClick` (the button handler) → `async void OnImportClick(...)`, with `await RunImportProcess()`

This is a mechanical change — nothing about the import logic itself changed, it just now sits inside an `async` method so it can `await` the dialog. If `drives.Count == 1` (the common case), the `await` resolves essentially instantly (no dialog shown at all) and the method proceeds exactly as it did before.

---

## 6. Side-by-Side: Export vs. Import Picker Usage

| | Export (`LetterPreviewDialog`) | Import (`ImportTab`) |
|---|---|---|
| Caller type | `Window` | `UserControl` |
| Owner for `ShowDialog` | `this` | `this.GetVisualRoot() as Window` |
| Cancel message | `"Export cancelled."` (black) | `"Import cancelled."` |
| After picking | writes to `<drive>/exports/` | reads from `<drive>/airpacks/` |
| Method signature change | none (`ExportToUSBAsync` was already `async Task`) | `RunImportProcess` became `async Task`, `OnImportClick` became `async void` |

---

## 7. Testing the Picker Specifically

The picker only appears with **2 or more** drives mounted simultaneously. To exercise it:

1. Plug in **two** USB drives, wait for both to auto-mount (`ls /media/$USER/` should show two folders)
2. Trigger Export (from a letter preview) or Import (Import Airpacks tab)
3. **Expected:** the "Multiple USB Drives Detected" dialog appears, listing both drives by label, first one pre-selected
4. Test **Select** with the default (first) drive → operation proceeds using that drive
5. Test **Select** after clicking the *second* radio button → operation proceeds using the second drive instead
6. Test **Cancel** → status shows "Export cancelled." / "Import cancelled.", no file is written/read, no error styling (black/default text, not red)

With only one drive plugged in, the picker should **never** appear — confirming the single-drive fast path still works unchanged is part of regression-testing this feature.

---

## 8. Design Notes / Why It's Built This Way

- **Radio buttons over a dropdown or click-to-select list**: chosen because the picker is rare (most users have exactly one USB stick) and when it *does* appear, the "you must explicitly choose one of these N things" framing should be unambiguous. A dropdown's default-selected-value can look like it's already a deliberate choice; radio buttons with one pre-checked make "this is the default, change it if you want" visually clear.
- **First drive pre-selected by default**: clicking Select immediately (without touching any radio button) reproduces the *old* "just use `drives[0]`" behavior — so the picker doesn't make the common "I don't care which, just go" case any slower than a single extra click.
- **No "remember my choice" option (yet)**: every multi-drive operation re-prompts. For an appliance that's normally used with one dedicated USB stick, this is expected to be rare enough that persistence isn't worth the added state/config surface. Could be revisited if multi-drive usage turns out to be common in practice.
- **Shared dialog for both flows**: one `UsbDrivePickerDialog` class, used identically by Export and Import. Keeps the UI consistent and means any future improvement (styling, "remember choice," drive free-space display, etc.) only needs to happen in one place.

---

*Last updated: June 15, 2026 — covers UsbDrivePickerDialog (#7), wired into both LetterPreviewDialog.ExportToUSBAsync and ImportTab.RunImportProcess. Confirmed to build and run without error; not yet tested with real multi-drive hardware.*
