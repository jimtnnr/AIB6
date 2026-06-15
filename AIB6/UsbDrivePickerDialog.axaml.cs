using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;
using AIB6.Helpers;

namespace AIB6
{
    /// <summary>
    /// Modal dialog shown when UsbDriveScanner finds more than one mounted drive.
    /// Lets the user pick which drive to use for Import or Export.
    /// Returns the selected mount path, or null if cancelled.
    /// </summary>
    public partial class UsbDrivePickerDialog : Window
    {
        private string? _selectedDrive;
        private readonly List<RadioButton> _radioButtons = new();

        public UsbDrivePickerDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Builds the dialog content from a list of detected drive mount paths.
        /// Call this immediately after construction, before ShowDialog.
        /// </summary>
        public void Populate(List<string> drivePaths)
        {
            RootStack.Children.Clear();
            _radioButtons.Clear();

            var title = new TextBlock
            {
                Text = "Multiple USB Drives Detected",
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            RootStack.Children.Add(title);

            var subtitle = new TextBlock
            {
                Text = "Select which drive to use:",
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            RootStack.Children.Add(subtitle);

            for (int i = 0; i < drivePaths.Count; i++)
            {
                var path = drivePaths[i];
                var label = UsbDriveScanner.GetDriveLabel(path);

                var radio = new RadioButton
                {
                    GroupName = "UsbDriveChoice",
                    Content = $"{label}  ({path})",
                    Tag = path,
                    IsChecked = i == 0
                };
                _radioButtons.Add(radio);
                RootStack.Children.Add(radio);
            }

            // Pre-select the first option by default
            _selectedDrive = drivePaths.FirstOrDefault();

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
                Spacing = 10
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(14, 6)
            };
            cancelButton.Click += (_, _) => Close(null);
            buttonPanel.Children.Add(cancelButton);

            var selectButton = new Button
            {
                Content = "Select",
                Padding = new Thickness(14, 6),
                Background = new SolidColorBrush(Color.Parse("#0078D7")),
                Foreground = Brushes.White,
                FontWeight = FontWeight.Bold,
                CornerRadius = new CornerRadius(4)
            };
            selectButton.Click += OnSelectClick;
            buttonPanel.Children.Add(selectButton);

            RootStack.Children.Add(buttonPanel);
        }

        private void OnSelectClick(object? sender, RoutedEventArgs e)
        {
            var chosen = _radioButtons.FirstOrDefault(r => r.IsChecked == true);
            _selectedDrive = chosen?.Tag as string ?? _selectedDrive;
            Close(_selectedDrive);
        }
    }
}
