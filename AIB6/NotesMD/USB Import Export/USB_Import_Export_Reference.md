# AirLock — USB Import/Export: Complete Reference

*How USB-based Airpack import and letter export works, what the machine needs, and how the code is wired together. Written so a new session (or a new person) can understand the whole pipeline without re-deriving it.*

---

## 1. The Big Picture

AirLock uses a USB drive for two one-way flows:

```
                    ┌─────────────────────┐
   Import:          │   USB DRIVE          │
   airpacks/  ───────►  /airpacks/*.airpack │──► copied into
   (.airpack files)  │                      │    ~/Documents/AIBDOCS/config/
                     │                      │
   Export:           │                      │
   letters  ◄─────────  /exports/*.txt      │◄── copied from
   (.txt files)      │                      │    ~/Documents/AIBDOCS/*.txt
                     └─────────────────────┘
```

- **Import** pulls `.airpack` workflow definition files **from** the USB **into** AirLock's local config folder.
- **Export** pushes generated letter `.txt` files **from** AirLock's local storage **onto** the USB.

Both operations expect a standard two-folder structure on the drive:

```
USB_DRIVE/
├── airpacks/      ← Import scans here for .airpack files
└── exports/       ← Export writes letters here
```

**Neither folder needs to exist beforehand.** The app creates both automatically the first time it needs them (`Directory.CreateDirectory` is idempotent — safe to call even if the folder already exists). So any blank, freshly-formatted USB stick works with zero prep.

---

## 2. Why This Is Hard on Linux (and how we solved it)

### The problem
Unlike Windows (`D:\`, `E:\`), Linux does not assign predictable drive letters or mount paths to USB drives. Plugging in a drive does **nothing** by default — no mount, no path, nothing for the app to read.

### The solution: udisks2 + udiskie
These are the standard Linux userspace tools that watch for new block devices and automatically mount them to a predictable *pattern* (not a fixed path, but a consistent pattern):

```
/media/<your-username>/<VOLUME_LABEL>/
```

`<VOLUME_LABEL>` is whatever the drive was named (e.g., a stick labeled "AIRLOCK" on Windows shows up as `/media/jim/AIRLOCK`).

### The app-side solution: UsbDriveScanner
Rather than hardcode a path (which breaks the moment the volume label changes, or a second drive is plugged in), AirLock's `UsbDriveScanner` class scans the common mount locations at the moment the user clicks Import or Export, and finds whatever is actually there. This is the "the app finds the drive, the user just plugs it in" principle from the original USB design doc.

---

## 3. One-Time Machine Setup (New Linux Install)

Run this once per machine (or bake it into the golden image):

```bash
# Install the automount tools
sudo apt update
sudo apt install udisks2 udiskie -y

# Run udiskie now (mounts any currently-plugged-in drives)
udiskie --no-tray &
```

### Make it permanent (start on every login)

```bash
mkdir -p ~/.config/autostart
cat > ~/.config/autostart/udiskie.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=udiskie
Exec=udiskie --no-tray
EOF
```

After this, `udiskie` runs silently in the background every time the user logs in, and any USB drive plugged in afterward auto-mounts within a second or two — no user action beyond physically inserting the drive.

### Verifying it's working

```bash
# Plug in a USB drive, wait ~2 seconds, then:
ls /media/$USER/
```

You should see a folder named after the drive's volume label. If you see nothing:

```bash
# Check udiskie is actually running
pgrep -a udiskie

# Check what got mounted, system-wide
mount | grep -i media
mount | grep -i /run/media

# Check kernel saw the device at all
lsblk
dmesg | tail -20
```

If `lsblk` shows the device (e.g. `sdb1`) but it's not mounted anywhere, `udiskie` isn't running or isn't catching the event — restart it (`pkill udiskie; udiskie --no-tray &`) and re-plug the drive.

---

## 4. How the Code Works

### 4.1 `UsbDriveScanner.cs` — the foundation

Location: `Helpers/UsbDriveScanner.cs`

This static class has no dependencies on `AppSettings` — it's pure filesystem scanning.

**`FindMountedDrives()`** — the core method. It checks three root locations, in order:

```csharp
private static readonly string[] MountRoots = new[]
{
    "/media",
    "/run/media",
    "/mnt"
};
```

For each root that exists, it recursively walks all subdirectories (`SearchOption.AllDirectories`) and keeps any directory that:
- exists
- is readable (no permission error)
- contains at least one file or folder (i.e., isn't empty)

That "non-empty directory" check is the heuristic for "this looks like a real mounted drive, not an empty system folder." It returns a deduplicated list of full paths, e.g.:

```
["/media/jim/AIRLOCK"]
```

or, with two drives:

```
["/media/jim/AIRLOCK", "/media/jim/KINGSTON"]
```

**`GetDriveLabel(path)`** — takes a full mount path and returns just the last segment (the volume label) for display: `/media/jim/AIRLOCK` → `"AIRLOCK"`.

**`GetSingleDriveOrNull()`** — convenience method, returns the one drive if exactly one is found, else `null`. (Currently unused by Export/Import directly — both call `FindMountedDrives()` and take `drives[0]`, see §4.4 on the "first drive" interim behavior.)

### 4.2 Export — `LetterPreviewDialog.cs`

The Export button (in the letter preview window) calls `ExportToUSBAsync()`. Step by step:

```csharp
private async Task ExportToUSBAsync()
{
    // 1. Show "Exporting..." in black immediately
    _statusText.Text = "Exporting...";

    // 2. Scan for drives
    var drives = UsbDriveScanner.FindMountedDrives();

    // 3. Zero drives → red error, stop
    if (drives.Count == 0)
    {
        _statusText.Text = "No USB drive found. Please insert a USB drive and try again.";
        _statusText.Foreground = Brushes.Red;
        return;
    }

    // 4. Pick the first drive found
    var selectedDrive = drives[0];

    // 5. Ensure exports/ exists on that drive (create if missing)
    var exportFolder = Path.Combine(selectedDrive, "exports");
    Directory.CreateDirectory(exportFolder);

    // 6. Copy the letter file there, overwriting if it already exists
    var destinationPath = Path.Combine(exportFolder, Path.GetFileName(_sourceFilePath));
    File.Copy(_sourceFilePath, destinationPath, overwrite: true);

    // 7. Show green success message with the real path
    _statusText.Text = $"Saved to {driveLabel}/exports/{filename}";
    _statusText.Foreground = Brushes.Green;
}
```

`_sourceFilePath` is the path to the already-saved letter on local disk (in `~/Documents/AIBDOCS/`), passed into the dialog's constructor when it's opened from the Review Drafts grid.

Any exception (permission denied, drive unplugged mid-copy, disk full) is caught and shown as a red "We couldn't save your letter. Please check your USB and try again."

### 4.3 Import — `ImportTab.axaml.cs`

The "Click - Import Airpack (.airpack)" button calls `RunImportProcess()`:

```csharp
private void RunImportProcess()
{
    // 1. Resolve where local .airpack configs live
    var configDir = Path.GetDirectoryName(
        Environment.ExpandEnvironmentVariables(Program.AppSettings.Paths.PromptTemplatesFile));
    // → ~/Documents/AIBDOCS/config

    // 2. Scan for drives
    var drives = UsbDriveScanner.FindMountedDrives();

    // 3. Zero drives → error, stop
    if (drives.Count == 0)
    {
        _statusText.Text = "No USB drive found. Please insert a USB drive and try again.";
        return;
    }

    // 4. Pick the first drive found
    var selectedDrive = drives[0];

    // 5. Ensure airpacks/ exists on that drive (create if missing)
    var importPath = Path.Combine(selectedDrive, "airpacks");
    Directory.CreateDirectory(importPath);

    // 6. Find .airpack files in that folder
    var importFiles = Directory.GetFiles(importPath, "*.airpack");
    if (importFiles.Length == 0)
    {
        _statusText.Text = $"No Airpack files found on \"{driveLabel}\". ...";
        return;
    }

    // 7. For each file: validate, then copy
    foreach (var file in importFiles)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(file));

        // Sigil check — reject anything not pack-signed
        if (sigil != "owl_440Hz_approved")
        {
            rejectedFiles.Add(fileName);
            continue;
        }

        // Skip if already imported (same filename exists locally)
        if (File.Exists(destinationFile))
        {
            skippedFiles.Add(fileName);
            continue;
        }

        // Copy into local config dir
        File.Copy(file, destinationFile);
        totalImported++;
    }

    // 8. Build a multi-line status report:
    //    ✅ Imported N new Airpack file(s) from "LABEL".
    //    Skipped: ...
    //    Rejected: ...
    //    Unreadable: ...
}
```

**Important: importing does NOT make new packs immediately usable.** `PromptTemplateRegistry.Load()` only runs once, at app startup (`Program.cs`). Copying a new `.airpack` file into the config folder makes it available on disk, but the dropdowns in the Create Drafts tab won't show it until the app is **restarted**. This isn't currently surfaced to the user — worth a status message addition ("Restart AirLock to use the new pack") if this becomes confusing in practice.

### 4.4 The "first drive found" behavior (current limitation)

Both Export and Import currently do this when multiple drives are detected:

```csharp
var selectedDrive = drives[0];
// ...
var noteSuffix = drives.Count > 1
    ? $" (multiple drives detected — used \"{driveLabel}\")"
    : "";
```

So if you have two USB drives plugged in, AirLock silently picks whichever one `FindMountedDrives()` happens to return first (order depends on filesystem enumeration order — not guaranteed to be insertion order) and tells you which one it used *after the fact*, in the success/result message.

**This is intentional as an interim step**, not a bug. The original design called for a drive-picker dialog (let the user choose *before* the operation runs) when multiple drives are present. That's tracked as a separate item — not yet built. For typical single-USB-stick operation, this has no effect at all.

---

## 5. The `_sigil` Validation Gate

Every `.airpack` file must contain:

```json
{
  "_sigil": "owl_440Hz_approved",
  ...
}
```

Files missing this field, or with a different value, are **rejected** (not imported, reported separately in the status message). This is a simple tamper/format check — it doesn't cryptographically verify anything, it just ensures the file is a deliberately-prepared Airpack rather than a random JSON file someone dropped in the `airpacks/` folder. The value itself is a fixed string baked into the validation logic — it's not meant to be a secret, just a marker.

---

## 6. Full End-to-End Test Procedure

Use this checklist when a USB drive is available:

### Setup (one-time)
```bash
sudo apt install udisks2 udiskie -y
udiskie --no-tray &
```

### Export test
1. Plug in USB, wait 2-3 seconds for auto-mount
2. Verify: `ls /media/$USER/` shows the drive
3. In AirLock: generate and save a letter (Create Drafts tab)
4. Go to Review Drafts, click View on the saved letter — opens the preview dialog
5. Click **Export**
6. **Expected:** green message `Saved to <LABEL>/exports/<filename>.txt`
7. Verify on disk: `ls /media/$USER/<LABEL>/exports/` shows the file

### Import test
1. With the same drive mounted, check/create the airpacks folder:
   ```bash
   mkdir -p /media/$USER/<LABEL>/airpacks
   ```
2. Copy a valid `.airpack` file (containing `"_sigil": "owl_440Hz_approved"`) into that folder
3. In AirLock: go to Import Airpacks tab, click **"Click - Import Airpack (.airpack)"**
4. **Expected:** green-ish message `✅ Imported 1 new Airpack file(s) from "<LABEL>".`
5. Verify on disk: `ls ~/Documents/AIBDOCS/config/` shows the new file
6. **Restart AirLock** for the new pack's letter types to appear in the Create Drafts dropdowns

### Error-path tests (no drive needed)
- Export with no USB plugged in → red "No USB drive found..."
- Import with no USB plugged in → "No USB drive found..."
- Import with USB plugged in but empty `airpacks/` folder → "No Airpack files found on..."
- Import a `.airpack` file with wrong/missing `_sigil` → reported under "Rejected"
- Import the same valid file twice → second run reports it under "Skipped"

---

## 7. Known Gaps / Next Steps

| Gap | Impact | Planned fix |
|---|---|---|
| Multiple drives → silently picks first | Low (rare with single-USB appliance use) | Drive picker dialog (separate item) |
| New `.airpack` requires app restart to appear in dropdowns | Medium — confusing first-time UX | Add "Restart AirLock to use this pack" to success message, or trigger `PromptTemplateRegistry.Load()` again post-import |
| `Paths.ImportFolder` / `Paths.ExportUSB` still in `appsettings.json` | None functionally — unused dead config | Remove from `AppSettings.cs` and `appsettings.json` |
| Temporary "Debug: Scan USB Drives" button still in Import tab | Cosmetic/dev-only | Remove once real flows are field-tested |
| `_sigil` is a static string, not cryptographic | Low — not a security boundary, just a format gate | None planned; acceptable for current trust model |

---

*Last updated: June 15, 2026 — covers UsbDriveScanner, real Export (#4), and airpacks/exports folder structure + Import rewrite (#6). Drive picker (#7) not yet implemented.*
