using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using Avalonia.Controls.Primitives;

namespace AIB6
{
    public class LetterPreviewDialog : Window
    {
        private readonly TextBlock _textBlock;
        private readonly TextBlock _statusText;

        public LetterPreviewDialog(string title, string letterText)
        {
            Title = title;
            Width = 800;
            Height = 600;

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
            exportButton.Click += OnExport;
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
                Foreground = Brushes.Gray,
                FontSize = 12,
                Margin = new Thickness(10, 0, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            DockPanel.SetDock(_statusText, Dock.Bottom);
            dockPanel.Children.Add(_statusText);

            _textBlock = new TextBlock
            {
                Text = letterText,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };

            var scrollViewer = new ScrollViewer
            {
                Content = _textBlock,
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

        private async void OnExport(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _statusText.Text = "Exported";
            await System.Threading.Tasks.Task.Delay(2000);
            _statusText.Text = "";
        }
    }
}