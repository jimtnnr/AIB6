using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AIB6.Helpers
{
    /// <summary>
    /// Detects mounted removable USB drives at runtime by scanning common
    /// Linux automount locations. No hardcoded paths — the app finds the drive,
    /// the user just plugs it in.
    /// </summary>
    public static class UsbDriveScanner
    {
        private static readonly string[] MountRoots = new[]
        {
            "/media",
            "/run/media",
            "/mnt"
        };

        /// <summary>
        /// Returns a list of candidate mount-point directories for removable drives.
        /// Looks under /media, /run/media, and /mnt (one or two levels deep, to
        /// account for udisks2-style /media/{user}/{LABEL} paths).
        /// </summary>
        public static List<string> FindMountedDrives()
        {
            var drives = new List<string>();

            foreach (var root in MountRoots)
            {
                if (!Directory.Exists(root))
                    continue;

                List<string> candidates;
                try
                {
                    candidates = Directory.GetDirectories(root, "*", SearchOption.AllDirectories).ToList();
                }
                catch
                {
                    continue;
                }

                foreach (var path in candidates)
                {
                    if (IsLikelyMountPoint(path))
                        drives.Add(path);
                }
            }

            return drives.Distinct().ToList();
        }

        /// <summary>
        /// Heuristic check: a directory is treated as a real USB mount point if
        /// it exists, is accessible, and is not empty.
        /// </summary>
        private static bool IsLikelyMountPoint(string path)
        {
            try
            {
                return Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length > 0;
            }
            catch
            {
                // Permission denied, broken symlink, race condition (drive unplugged), etc.
                return false;
            }
        }

        /// <summary>
        /// Convenience: returns the single mounted drive if exactly one is found,
        /// otherwise null. Caller should check FindMountedDrives().Count to decide
        /// whether to show a picker or an error message.
        /// </summary>
        public static string? GetSingleDriveOrNull()
        {
            var drives = FindMountedDrives();
            return drives.Count == 1 ? drives[0] : null;
        }

        /// <summary>
        /// Returns a friendly display label for a mount point — typically the
        /// final path segment (the volume label).
        /// </summary>
        public static string GetDriveLabel(string mountPath)
        {
            var trimmed = mountPath.TrimEnd('/');
            var label = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(label) ? trimmed : label;
        }
    }
}
