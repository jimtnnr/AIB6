using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using System.IO;
using System.Threading.Tasks;

namespace AIB6
{
    public class LetterPreviewDialog : Window
    {
        private readonly TextBlock _textBlock;
        private readonly TextBlock _statusText;
        private readonly string _sourceFilePath;

        public LetterPreviewDialog(string title, string letterText, string filePath)
        {
            Title = title;
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _sourceFilePath = filePath;

            var dockPanel = new DockPanel();

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10),
                Spacing = 10
            };

            var exportButton = new Button
            {
                Content = "Export",
                Padding = new Thickness(10, 5),
                Background = new SolidColorBrush(Color.Parse("#0078D7")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                CornerRadius = new CornerRadius(4)
            };
            exportButton.Click += async (_, _) => await ExportToUSBAsync();
            buttonPanel.Children.Add(exportButton);

            var closeButton = new Button
            {
                Content = "Close",
                Padding = new Thickness(10, 5),
                Background = new SolidColorBrush(Color.Parse("#0078D7")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                CornerRadius = new CornerRadius(4)
            };
            closeButton.Click += OnClose;
            buttonPanel.Children.Add(closeButton);

            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            dockPanel.Children.Add(buttonPanel);

            _statusText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.Parse("#0078D4")),
                FontWeight = FontWeight.Bold,
                FontSize = 14,
                Margin = new Thickness(10, 0, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 1000
            };

            DockPanel.SetDock(_statusText, Dock.Bottom);
            dockPanel.Children.Add(_statusText);

            _textBlock = new TextBlock
            {
                Text = letterText,
                FontSize = 16,
                FontWeight = FontWeight.Normal,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            var scrollViewer = new ScrollViewer
            {
                Content = _textBlock,
                Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            DockPanel.SetDock(scrollViewer, Dock.Top);
            dockPanel.Children.Add(scrollViewer);

            Content = dockPanel;
        }

        private void OnClose(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        private async Task ExportToUSBAsync()
        {
            try
            {
                var exportFolder = Program.AppSettings.Paths.ExportUSB;

                if (!Directory.Exists(exportFolder))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                        _statusText.Text = "No USB drive found. Please insert a USB stick and try again.");
                    return;
                }

                var destinationPath = Path.Combine(exportFolder, Path.GetFileName(_sourceFilePath));

                if (!File.Exists(destinationPath))
                {
                    File.Copy(_sourceFilePath, destinationPath, overwrite: true);
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                    _statusText.Text = "Letter saved to your USB drive.");
            }
            catch (Exception)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _statusText.Text = "We couldn’t save your letter. Please check your USB and try again.");
            }
        }
    }
}
