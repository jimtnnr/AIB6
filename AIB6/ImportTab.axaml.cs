using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Avalonia;
using AIB6.Helpers;

namespace AIB6
{
    public partial class ImportTab : UserControl
    {
        private StackPanel _mainPanel;
        private TextBlock _statusText;
        private Button _importButton;
        private Button _scanUsbButton;

        public ImportTab()
        {
            InitializeComponent();

            _mainPanel = new StackPanel { Margin = new Thickness(20) };
            ImportStack.Children.Clear();
            ImportStack.Children.Add(_mainPanel);

            var title = new TextBlock
            {
                Text = "Import Airpacks",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var instructions = new TextBlock
            {
                Text = "Import new Airpack workflow files.",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20)
            };

            _importButton = new Button
            {
                Content = "Click - Import Airpack (.airpack)",
                Width = 300,
                Height = 50,
                Background = new SolidColorBrush(Color.Parse("#F7630C")),
                Foreground = Brushes.White
            };
            _importButton.Click += OnImportClick;

            // TEMPORARY DEBUG BUTTON — remove once UsbDriveScanner is wired into
            // real Import/Export flows (items #4-7). Lets us test drive detection
            // directly from inside the app without a separate console project.
            _scanUsbButton = new Button
            {
                Content = "Debug: Scan USB Drives",
                Width = 300,
                Height = 40,
                Margin = new Thickness(0, 10, 0, 0),
                Background = new SolidColorBrush(Color.Parse("#666666")),
                Foreground = Brushes.White
            };
            _scanUsbButton.Click += OnScanUsbClick;

            _statusText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 12,
                Margin = new Thickness(0, 20, 0, 0)
            };

            _mainPanel.Children.Add(title);
            _mainPanel.Children.Add(instructions);
            _mainPanel.Children.Add(_importButton);
            _mainPanel.Children.Add(_scanUsbButton);
            _mainPanel.Children.Add(_statusText);
        }

        private void OnImportClick(object? sender, RoutedEventArgs e)
        {
            RunImportProcess();
        }

        private void OnScanUsbClick(object? sender, RoutedEventArgs e)
        {
            var drives = UsbDriveScanner.FindMountedDrives();

            if (drives.Count == 0)
            {
                _statusText.Text = "Scan result: No USB drives detected.";
                return;
            }

            var lines = new List<string> { $"Scan result: {drives.Count} drive(s) found:" };
            foreach (var drive in drives)
            {
                lines.Add($"- {UsbDriveScanner.GetDriveLabel(drive)}  ({drive})");
            }

            _statusText.Text = string.Join("\n", lines);
        }

        private void RunImportProcess()
        {
            try
            {
                var importPath = Program.AppSettings.Paths.ImportFolder;
                var templatePath = Environment.ExpandEnvironmentVariables(Program.AppSettings.Paths.PromptTemplatesFile);
                var configDir = Path.GetDirectoryName(templatePath);

                if (string.IsNullOrWhiteSpace(importPath) || !Directory.Exists(importPath))
                {
                    _statusText.Text = "No import folder detected. Check that your USB is plugged in and contains the required folder.";
                    return;
                }

                var importFiles = Directory.GetFiles(importPath, "*.airpack");
                if (importFiles.Length == 0)
                {
                    _statusText.Text = "No Airpack files found. Make sure your USB includes at least one '.airpack' file.";
                    return;
                }

                int totalImported = 0;
                List<string> skippedFiles = new();
                List<string> rejectedFiles = new();
                List<string> unreadableFiles = new();

                foreach (var file in importFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var json = File.ReadAllText(file);

                    try
                    {
                        using var doc = JsonDocument.Parse(json);

                        if (!doc.RootElement.TryGetProperty("_sigil", out var sigil) || sigil.GetString() != "owl_440Hz_approved")
                        {
                            rejectedFiles.Add(fileName);
                            continue;
                        }

                        var destinationFile = Path.Combine(configDir ?? "", fileName);
                        if (File.Exists(destinationFile))
                        {
                            skippedFiles.Add(fileName);
                            continue;
                        }

                        File.Copy(file, destinationFile);
                        totalImported++;
                    }
                    catch (Exception ex)
                    {
                        unreadableFiles.Add($"{fileName}: {ex.Message.Split('\n')[0]}");
                    }
                }

                var messageLines = new List<string>
                {
                    $"✅ Imported {totalImported} new Codex file(s)."
                };

                if (skippedFiles.Count > 0)
                {
                    messageLines.Add($"Skipped {skippedFiles.Count} file(s) — already imported:\n- {string.Join("\n- ", skippedFiles)}");
                }

                if (rejectedFiles.Count > 0)
                {
                    messageLines.Add($"Rejected {rejectedFiles.Count} file(s) — invalid or unverified format:\n- {string.Join("\n- ", rejectedFiles)}");
                }

                if (unreadableFiles.Count > 0)
                {
                    messageLines.Add($"Unreadable {unreadableFiles.Count} file(s) — possibly corrupted:\n- {string.Join("\n- ", unreadableFiles)}");
                }

                _statusText.Text = string.Join("\n\n", messageLines);
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error during import: {ex.Message}";
            }
        }
    }
}
