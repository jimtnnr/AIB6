using Avalonia.Controls;

namespace AIB6
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var tabControl = this.FindControl<TabControl>("MainTabs");
            if (tabControl != null)
            {
                tabControl.SelectionChanged += OnTabChanged;
            }
        }
        private async void OnTabChanged(object? sender, SelectionChangedEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl?.SelectedItem is TabItem selectedTab)
            {
                //Console.WriteLine($"TAB CHANGED: {selectedTab.Header}");
                if (selectedTab.Header?.ToString() == "Review Drafts")
                {
                   // Console.WriteLine("Review Drafts tab activated.");
                    var archiveGrid = this.FindControl<ArchiveGridView>("ArchiveGridView");
                    if (archiveGrid != null)
                        await archiveGrid.RefreshGridAsync(); // Or sort here if needed
                }
            }
        }

    }
}