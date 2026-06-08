# AirLock – USB Import & Export: Problem Analysis & Solution Plan

---

## The Problem in Plain English

AirLock currently has two USB-dependent workflows:

1. **Import Airpacks** — user plugs in a USB drive containing `.airpac` files, clicks Import, packs load into the system
2. **Export Letters** — user clicks Export in the letter preview, letter saves to USB drive

Both are currently **hardcoded to fixed paths** and have **no USB detection logic**. If the USB isn't mounted exactly where the code expects, it silently fails or shows a misleading message. This is the "loosey goosey" problem.

---

## Current State — What the Code Actually Does

### Import (ImportTab.axaml.cs)

```csharp
var importPath = Program.AppSettings.Paths.ImportFolder;
// Currently: /mnt/AIBUSB/import
```

- Scans `/mnt/AIBUSB/import` for `*.aibcodex` files
- If folder doesn't exist: shows error message
- **No USB detection. No drive enumeration. No mount handling.**
- If user has two USB drives, wrong one plugged in, or drive mounts to `/mnt/sdb1` instead of `/mnt/AIBUSB` — it just says "No import folder detected"

### Export (LetterPreviewDialog.cs)

```csharp
private async Task ExportToUSBAsync()
{
    await Task.Delay(1750);
    _statusText.Text = "Export Complete";  // ← THIS IS FAKE
}
```

**The export button is currently a simulation. It does not copy the file anywhere.** The real implementation (`ExportToUSBAsyncOld`) is commented out. The working version checks `/mnt/AIBUSB/export` but has the same hardcoded path problem.

---

## The Linux USB Mounting Problem

This is the core technical issue. Linux does not auto-mount USB drives to predictable paths the way Windows does (D:\, E:\ etc).

### What Linux Actually Does

When a USB drive is inserted:
- `udev` detects the device
- The kernel assigns a block device: `/dev/sdb`, `/dev/sdc`, `/dev/sdd` etc.
- The device number depends on insertion order — **not predictable**
- Without automount configured, the drive is **not mounted at all**
- With automount (udisks2/udiskie), it mounts to: `/media/jim/DRIVE_LABEL` or `/run/media/jim/DRIVE_LABEL`
- The label in the path is the **volume label of the USB drive** — whatever it was named on Windows/Mac

### Why This Breaks Airlock

| Scenario | Result |
|---|---|
| USB labeled "AIBUSB" mounted at `/mnt/AIBUSB` | Works (by coincidence) |
| USB labeled "USB_DRIVE" mounted at `/media/jim/USB_DRIVE` | Fails — wrong path |
| Two USB drives inserted | Unpredictable which one gets scanned |
| USB not yet mounted when button clicked | Fails silently |
| USB mounted but wrong folder structure | Fails with confusing message |

---

## Proposed Solution

### Principle

**Do not hardcode paths. Detect USB drives dynamically at click time.**

The app should find the USB drive, not expect the user to have mounted it to the right place.

---

### Solution Architecture

#### Step 1 — Linux USB Auto-Mount Setup (One-Time Machine Config)

Install and enable `udisks2` and `udiskie` on the Airlock machine:

```bash
sudo apt install udisks2 udiskie -y
```

Add udiskie to autostart so USB drives mount automatically on insert:

```bash
# Add to ~/.config/autostart/ or systemd user service
udiskie --no-tray &
```

This ensures any USB inserted mounts automatically to:
```
/media/jim/VOLUME_LABEL/
```

No manual mounting required. Drive appears as soon as it's plugged in.

---

#### Step 2 — USB Drive Detection at Runtime

Replace the hardcoded path with a **USB scanner** that finds all mounted removable drives at click time:

```csharp
public static class UsbDriveScanner
{
    private static readonly string[] MountRoots = new[]
    {
        "/media",
        "/run/media",
        "/mnt"
    };

    public static List<string> FindMountedDrives()
    {
        var drives = new List<string>();

        foreach (var root in MountRoots)
        {
            if (!Directory.Exists(root)) continue;

            // /media/username/DRIVELABEL or /media/DRIVELABEL
            foreach (var path in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                // Filter to actual mount points (has files, not empty system dirs)
                try
                {
                    if (Directory.GetFileSystemEntries(path).Length > 0)
                        drives.Add(path);
                }
                catch { }
            }
        }

        return drives.Distinct().ToList();
    }
}
```

---

#### Step 3 — Multiple Drive Handling

If more than one USB drive is found, **show a picker dialog** rather than guessing:

```
┌─────────────────────────────────────────┐
│  Select USB Drive                        │
│                                          │
│  ○ AIBUSB  (/media/jim/AIBUSB)          │
│  ○ KINGSTON  (/media/jim/KINGSTON)      │
│                                          │
│  [Select]  [Cancel]                      │
└─────────────────────────────────────────┘
```

If only one drive is found, skip the picker and use it directly.
If no drives are found, show a clear actionable message: "No USB drive detected. Please insert a USB drive and try again."

---

#### Step 4 — Standardize Folder Structure on USB

Define a standard Airpack USB folder structure that the app expects:

```
USB_DRIVE/
├── airpacks/          ← .airpac files go here
└── exports/           ← exported letters land here
```

On import: scan `{selectedDrive}/airpacks/` for `*.airpac` files
On export: write to `{selectedDrive}/exports/{filename}`

If the folders don't exist on the USB, **create them automatically** on first use. This means any USB drive works — the user doesn't need to pre-format or pre-structure it.

---

#### Step 5 — Export Fix (Remove the Simulation)

Replace the fake export with a real one using the detected drive path:

```csharp
private async Task ExportToUSBAsync()
{
    var drives = UsbDriveScanner.FindMountedDrives();

    if (drives.Count == 0)
    {
        _statusText.Text = "No USB drive found. Please insert a USB drive and try again.";
        _statusText.Foreground = Brushes.Red;
        return;
    }

    string selectedDrive;
    if (drives.Count == 1)
    {
        selectedDrive = drives[0];
    }
    else
    {
        // Show picker dialog — user selects which drive
        selectedDrive = await ShowDrivePickerAsync(drives);
        if (selectedDrive == null) return;
    }

    var exportFolder = Path.Combine(selectedDrive, "exports");
    Directory.CreateDirectory(exportFolder);

    var destinationPath = Path.Combine(exportFolder, Path.GetFileName(_sourceFilePath));
    File.Copy(_sourceFilePath, destinationPath, overwrite: true);

    _statusText.Text = $"Saved to {selectedDrive}/exports/";
    _statusText.Foreground = Brushes.Green;
}
```

---

## appsettings.json Cleanup

Remove the hardcoded USB paths from appsettings.json entirely. They are no longer needed once dynamic detection is in place:

```json
// REMOVE THESE:
"ImportFolder": "/mnt/AIBUSB/import",
"ExportUSB": "/mnt/AIBUSB/export"
```

The app detects USB drives at runtime. Paths are not configuration — they are discovered.

---

## UI Changes Required

### Import Tab
- Title: "Import Templates" → "Import Airpacks"
- Button: "Click - Import Codex (.aibcodex)" → "Import Airpack (.airpac)"
- Instructions: explain the USB folder structure (`airpacks/` subfolder)
- Status messages: clearer, more specific

### Export Button (LetterPreviewDialog)
- Remove simulation delay
- Show actual destination path on success
- Show drive picker if multiple drives detected
- Show red error if no drive found

---

## Tech Debt Items to Implement

| # | Item | Effort | Priority |
|---|---|---|---|
| 1 | Install + configure udiskie automount on Airlock machine image | 30 min | High — do at golden image time |
| 2 | Build `UsbDriveScanner` utility class | 2 hours | High |
| 3 | Fix Export — remove simulation, implement real file copy | 1 hour | High — currently broken |
| 4 | Build drive picker dialog for multiple USB scenario | 2 hours | Medium |
| 5 | Standardize USB folder structure (`airpacks/` + `exports/`) | 30 min | Medium |
| 6 | Update Import to scan `airpacks/` subfolder | 1 hour | Medium |
| 7 | Rename `.aibcodex` → `.airpac` throughout | 30 min | Medium (see tech debt doc) |
| 8 | Remove hardcoded USB paths from appsettings.json | 15 min | Low — after items above |
| 9 | Update UI labels (Import Codex → Import Airpack) | 15 min | Low |

---

## Recommended Build Order

1. Configure udiskie on the machine (one-time, do during golden image build)
2. Build `UsbDriveScanner` — shared utility used by both Import and Export
3. Fix Export first — it's currently non-functional, highest user-facing impact
4. Fix Import second — scanner logic is mostly already correct, just needs dynamic path
5. Add drive picker dialog
6. Rename `.aibcodex` → `.airpac` as part of the same session
7. Clean up appsettings.json

---

## The Golden Rule for USB in AirLock

> The app finds the drive. The user just plugs it in.

The operator is not a Linux sysadmin. They should never need to know what `/mnt/AIBUSB` is. They plug in a USB, click Import or Export, and it works. If something goes wrong, the message tells them exactly what to do next.

---

*Last updated: June 2026*
