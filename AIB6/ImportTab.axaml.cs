using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
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
            try
            {
                var importPath = Program.AppSettings.Paths.ImportFolder;

                if (string.IsNullOrWhiteSpace(importPath) || !Directory.Exists(importPath))
                {
                    _statusText.Text = "Import folder not found on USB.";
                    return;
                }

                var codexFiles = Directory.GetFiles(importPath, "*.aibcodex");

                if (codexFiles.Length == 0)
                {
                    _statusText.Text = "No .aibcodex files found in USB import folder.";
                }
                else
                {
                    var fileList = string.Join("\n", codexFiles.Select(Path.GetFileName));
                    _statusText.Text = $"Found {codexFiles.Length} file(s):\n{fileList}";
                }
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Error during import scan: {ex.Message}";
            }
        }

    }
}