using AIB6.Helpers;
using Avalonia.Controls;
using System.Linq;

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

            var templates = PromptTemplateRegistry.GetAllTemplates()?.ToList();
            if (templates is { Count: > 0 })
            {
                var defaultTemplate = templates.First();
                if (!string.IsNullOrWhiteSpace(defaultTemplate.Title))
                    this.Title = defaultTemplate.Title;
            }
        }

        private async void OnTabChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is TabControl tabControl &&
                tabControl.SelectedItem is TabItem selectedTab &&
                selectedTab.Header?.ToString() == "Review Drafts")
            {
                var archiveGrid = this.FindControl<ArchiveGridView>("ArchiveGridView");
                if (archiveGrid != null)
                    await archiveGrid.RefreshGridAsync();
            }
        }
    }
}