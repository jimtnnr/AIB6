using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
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

        public ImportTab()
        {
            InitializeComponent();

            _mainPanel = new StackPanel { Margin = new Thickness(20) };
            ImportStack.Children.Clear();
            ImportStack.Children.Add(_mainPanel);

            var title = new TextBlock
            {
                Text = "Import Codex Packs",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var instructions = new TextBlock
            {
                Text = "Import new prompt templates.",
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 20)
            };

            _importButton = new Button
            {
                Content = "Click - Import Codex (.aibcodex)",
                Width = 300,
                Height = 50,
                Background = new SolidColorBrush(Color.Parse("#F7630C")),
                Foreground = Brushes.White
            };
            _importButton.Click += OnImportClick;

            _statusText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 12,
                Margin = new Thickness(0, 20, 0, 0)
            };

            _mainPanel.Children.Add(title);
            _mainPanel.Children.Add(instructions);
            _mainPanel.Children.Add(_importButton);
            _mainPanel.Children.Add(_statusText);
        }

     
        private void OnImportClick(object? sender, RoutedEventArgs e)
        {
            RunImportProcess();
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
                    _statusText.Text = "Import folder not found on USB.";
                    return;
                }

                var importFiles = Directory.GetFiles(importPath, "*.aibcodex");
                if (importFiles.Length == 0)
                {
                    _statusText.Text = "No .json files found in USB import folder.";
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
                    $"Imported {totalImported} new template(s)."
                };

                if (skippedFiles.Count > 0)
                {
                    messageLines.Add($"\nSkipped {skippedFiles.Count} file(s) (already present):\n- {string.Join("\n- ", skippedFiles)}");
                }

                if (rejectedFiles.Count > 0)
                {
                    messageLines.Add($"\nRejected {rejectedFiles.Count} file(s) (not genuine AIB file):\n- {string.Join("\n- ", rejectedFiles)}");
                }

                if (unreadableFiles.Count > 0)
                {
                    messageLines.Add($"\nUnreadable {unreadableFiles.Count} file(s):\n- {string.Join("\n- ", unreadableFiles)}");
                }

                _statusText.Text = string.Join("\n", messageLines);
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error during import: {ex.Message}";
            }
        }
    }
}
    
