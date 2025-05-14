using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AIB6
{
    public partial class ArchiveGridView : UserControl
    {
        private int _currentPage = 1;
        private const int PageSize = 15;
        private string _sortColumn = "timestamp";
        private string _sortDirection = "DESC";
        private string _filter = "";
        private bool _showHidden = false;
        private readonly string _connectionString = Program.AppSettings.ConnectionStrings.Postgres;

        private TextBox _searchBox;
        private ToggleSwitch _toggleShowHidden;
        private StackPanel _mainPanel;
        private Grid _archiveGrid;
        private TextBlock PageLabel;

        public ArchiveGridView()
        {
            InitializeComponent();

            _mainPanel = new StackPanel();
            ArchiveStack.Children.Clear();
            ArchiveStack.Children.Add(_mainPanel);

            var searchRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Margin = new Thickness(0, 0, 0, 10)
            };

            _searchBox = new TextBox
            {
                Watermark = "Search...",
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _searchBox.KeyUp += OnFilterChanged;
            searchRow.Children.Add(_searchBox);
            Grid.SetColumn(_searchBox, 0);

            _toggleShowHidden = new ToggleSwitch
            {
                Content = "Show Hidden",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsChecked = _showHidden
            };
            _toggleShowHidden.Checked += async (_, _) => { _showHidden = true; await LoadPage(_currentPage); };
            _toggleShowHidden.Unchecked += async (_, _) => { _showHidden = false; await LoadPage(_currentPage); };
            searchRow.Children.Add(_toggleShowHidden);
            Grid.SetColumn(_toggleShowHidden, 1);

            _mainPanel.Children.Add(searchRow);
            _archiveGrid = new Grid();
            _mainPanel.Children.Add(new ScrollViewer { Content = _archiveGrid });

            _ = LoadPage(_currentPage);
        }

        private async void OnFilterChanged(object? sender, KeyEventArgs e)
        {
            _filter = _searchBox.Text ?? "";
            _currentPage = 1;
            await LoadPage(_currentPage);
        }

        private async void OnPreviousPage(object? sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
                await LoadPage(_currentPage - 1);
        }

        private async void OnNextPage(object? sender, RoutedEventArgs e)
        {
            await LoadPage(_currentPage + 1);
        }

        public async Task LoadPage(int page)
        {
            var items = await LoadDraftArchivePage(page, PageSize, _sortColumn, _sortDirection, _filter, _showHidden);

            if (items.Count == 0 && page > 1)
                return;

            _archiveGrid.RowDefinitions.Clear();
            _archiveGrid.Children.Clear();
            _archiveGrid.ColumnDefinitions.Clear();

            _archiveGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _archiveGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _archiveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            _archiveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            _archiveGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            _archiveGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            void AddHeader(string text, string columnKey, int colIndex, HorizontalAlignment align)
            {
                var label = new TextBlock
                {
                    Text = (_sortColumn == columnKey ? text + (_sortDirection == "ASC" ? " ↑" : " ↓") : text),
                    Classes = { "header-text" },
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = align,
                    TextAlignment = align == HorizontalAlignment.Left ? TextAlignment.Left : TextAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    IsHitTestVisible = true
                };

                label.PointerPressed += async (_, _) => await Sort(columnKey);

                Grid.SetColumn(label, colIndex);
                Grid.SetRow(label, 0);
                _archiveGrid.Children.Add(label);
            }

            AddHeader("Filename", "filename", 0, HorizontalAlignment.Left);
            AddHeader("Timestamp", "timestamp", 1, HorizontalAlignment.Left);
            AddHeader("Star", "favorite", 2, HorizontalAlignment.Center);
            AddHeader("Hide", "hidden", 3, HorizontalAlignment.Center);
            var empty = new TextBlock();
            Grid.SetColumn(empty, 4);
            Grid.SetRow(empty, 0);
            _archiveGrid.Children.Add(empty);

            int rowIndex = 1;
            foreach (var row in items)
            {
                _archiveGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var background = (rowIndex % 2 == 0)
                    ? new SolidColorBrush(Color.Parse("#E0F0FF"))
                    : new SolidColorBrush(Colors.White);

                void AddCell(Control control, int col)
                {
                    var border = new Border
                    {
                        Background = background,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = control
                    };
                    Grid.SetColumn(border, col);
                    Grid.SetRow(border, rowIndex);
                    _archiveGrid.Children.Add(border);
                }

                AddCell(new TextBlock
                {
                    Text = row.filename,
                    Foreground = Brushes.Black,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                }, 0);

                AddCell(new TextBlock
                {
                    Text = row.timestamp.ToString("g"),
                    Foreground = Brushes.Black,
                    FontWeight = FontWeight.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                }, 1);

                var star = new TextBlock
                {
                    Text = row.favorite ? "★" : "☆",
                    Foreground = row.favorite ? Brushes.Gold : Brushes.Gray,
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                star.PointerPressed += async (_, _) =>
                {
                    int currentPage = _currentPage;
                    int pinnedId = row.id;

                    await ToggleFavorite(pinnedId, !row.favorite);
                    await LoadPage(currentPage);

                    // Optional: scroll or highlight pinnedId row
                };

                AddCell(star, 2);

                var hiddenCheck = new CheckBox { IsChecked = row.hidden, HorizontalAlignment = HorizontalAlignment.Center };
                hiddenCheck.IsCheckedChanged += async (sender, _) =>
                {
                    if (sender is CheckBox cb)
                    {
                        int pinnedId = row.id;
                        int currentPage = _currentPage;

                        await ToggleHidden(pinnedId, cb.IsChecked == true);
                        await LoadPage(currentPage);
                    }
                };

                AddCell(hiddenCheck, 3);

                var previewLink = new TextBlock
                {
                    Text = "View",
                    Foreground = new SolidColorBrush(Color.Parse("#F7630C")),
                    FontWeight = FontWeight.Bold,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 6)
                };
                previewLink.PointerPressed += async (_, _) =>
                {
                    var docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AIBDOCS");
                    var filePath = Path.Combine(docsPath, row.filename);

                    string letterText = File.Exists(filePath)
                        ? await File.ReadAllTextAsync(filePath)
                        : $"""
                            Dear Sir or Madam,

                            This is a sample letter loaded from a fake backend at {DateTime.Now}.

                            Filename: {row.filename}
                            Type: {row.letter_type}

                            Regards,
                            TestBot
                          """;

                    var modal = new LetterPreviewDialog("Preview: " + row.filename, letterText)
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    var ownerWindow = this.VisualRoot as Window;
                    if (ownerWindow is not null)
                        await modal.ShowDialog(ownerWindow);

                };
                AddCell(previewLink, 4);

                rowIndex++;
            }

            if (_mainPanel.Children.Count > 2)
                _mainPanel.Children.RemoveAt(_mainPanel.Children.Count - 1);

            var pagerGrid = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                }
            };

            var pagerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var prevButton = new Button
            {
                Content = "Previous",
                Background = new SolidColorBrush(Color.Parse("#0078D4")),
                Foreground = Brushes.White,
                Padding = new Thickness(10, 5)
            };
            prevButton.Click += OnPreviousPage;
            pagerStack.Children.Add(prevButton);

            PageLabel = new TextBlock
            {
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            pagerStack.Children.Add(PageLabel);

            var nextButton = new Button
            {
                Content = "Next",
                Background = new SolidColorBrush(Color.Parse("#0078D4")),
                Foreground = Brushes.White,
                Padding = new Thickness(10, 5)
            };
            nextButton.Click += OnNextPage;
            pagerStack.Children.Add(nextButton);

            Grid.SetColumn(pagerStack, 1);
            pagerGrid.Children.Add(pagerStack);
            _mainPanel.Children.Add(pagerGrid);

            PageLabel.Text = $"Page {_currentPage = page}";
        }

        private async Task Sort(string column)
        {
            if (_sortColumn == column)
                _sortDirection = _sortDirection == "ASC" ? "DESC" : "ASC";
            else
            {
                _sortColumn = column;
                _sortDirection = "ASC";
            }

            await LoadPage(_currentPage);
        }

        private async Task<List<LetterMetadata>> LoadDraftArchivePage(int page, int pageSize, string sortColumn, string sortDirection, string filter, bool showHidden)
        {
            var results = new List<LetterMetadata>();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("SELECT * FROM get_draft_archive_page(@page, @size, @sort_column, @sort_direction, @filter, @show_hidden)", conn);
            cmd.Parameters.AddWithValue("page", page);
            cmd.Parameters.AddWithValue("size", pageSize);
            cmd.Parameters.AddWithValue("sort_column", sortColumn);
            cmd.Parameters.AddWithValue("sort_direction", sortDirection);
            cmd.Parameters.AddWithValue("filter", filter);
            cmd.Parameters.AddWithValue("show_hidden", showHidden);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new LetterMetadata
                {
                    id = reader.GetInt32(0),
                    filename = reader.GetString(1),
                    letter_type = reader.GetString(2),
                    timestamp = reader.GetDateTime(3),
                    favorite = reader.GetBoolean(4),
                    hidden = reader.GetBoolean(5),
                });
            }

            return results;
        }

        private async Task ToggleFavorite(int id, bool value)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("UPDATE draft_archive SET favorite = @val WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("val", value);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task ToggleHidden(int id, bool value)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand("UPDATE draft_archive SET hidden = @val WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("val", value);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        public async Task RefreshGridAsync()
        {
            await LoadPage(_currentPage); // or 1, if you want reset
        }


    }

    public class LetterMetadata
    {
        public int id { get; set; }
        public string filename { get; set; } = string.Empty;
        public string letter_type { get; set; } = string.Empty;
        public DateTime timestamp { get; set; }
        public bool favorite { get; set; }
        public bool hidden { get; set; }
    }
}
