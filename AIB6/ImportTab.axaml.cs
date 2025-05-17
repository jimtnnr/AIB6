using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Text.Json;
using Avalonia;

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

                var importFiles = Directory.GetFiles(importPath, "*.json");
                if (importFiles.Length == 0)
                {
                    _statusText.Text = "No .json files found in USB import folder.";
                    return;
                }

                List<PromptTemplate> existingTemplates = new();
                if (File.Exists(templatePath))
                {
                    var existingJson = File.ReadAllText(templatePath);
                    existingTemplates = JsonSerializer.Deserialize<List<PromptTemplate>>(existingJson) ?? new();
                }

                int totalImported = 0;
                List<string> skippedFiles = new();
                List<string> rejectedFiles = new();
                List<string> unreadableFiles = new();

                foreach (var file in importFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var destinationFile = Path.Combine(configDir ?? "", fileName);

                    if (File.Exists(destinationFile))
                    {
                        skippedFiles.Add(fileName);
                        continue;
                    }

                    try
                    {
                        var json = File.ReadAllText(file);
                        var singleTemplate = JsonSerializer.Deserialize<PromptTemplate>(json);

                        if (singleTemplate == null || singleTemplate._sigil != "owl_440Hz_approved")
                        {
                            rejectedFiles.Add(fileName);
                            continue;
                        }

                        existingTemplates.Add(singleTemplate);
                        totalImported++;
                    }
                    catch (Exception ex)
                    {
                        unreadableFiles.Add($"{fileName}: {ex.Message.Split('\n')[0]}");
                    }
                }

                var updatedJson = JsonSerializer.Serialize(existingTemplates, new JsonSerializerOptions { WriteIndented = true });
                if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
                File.WriteAllText(templatePath, updatedJson);

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

    public class PromptTemplate
    {
        public string Title { get; set; } = string.Empty;
        public string MainType { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string InputScaffold { get; set; } = string.Empty;
        public List<LengthOption> LengthOptions { get; set; } = new();
        public Dictionary<string, string> ToneDirectives { get; set; } = new();
        public string PromptTemplateText { get; set; } = string.Empty;
        public string RoleInstruction { get; set; } = string.Empty;
        public string _sigil { get; set; } = string.Empty;
    }

    public class LengthOption
    {
        public string Label { get; set; } = string.Empty;
        public int Words { get; set; }
    }
}
    
