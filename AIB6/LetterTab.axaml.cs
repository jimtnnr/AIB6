using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIB6;
using AIB6.Helpers;

namespace AIB6
{
    public partial class LetterTab : UserControl
    {
        private string _selectedModel;
        private string _apiUrl;

        public LetterTab()
        {
            InitializeComponent();

            var defaultModelKey = Program.AppSettings.ModelSettings.DefaultModel;
            if (defaultModelKey == "mixtral")
            {
                _selectedModel = Program.AppSettings.ModelSettings.Mixtral.ModelName;
                _apiUrl = Program.AppSettings.ModelSettings.Mixtral.Endpoint;
            }
            else
            {
                _selectedModel = Program.AppSettings.ModelSettings.Mistral.ModelName;
                _apiUrl = Program.AppSettings.ModelSettings.Mistral.Endpoint;
            }

            LetterTypeDropdown.ItemsSource = new[] { "Notice", "Demand", "Inquiry", "Confirmation" };
            ToneDropdown.ItemsSource = new[] { "Friendly", "Professional", "Stern" };
            FormalityDropdown.ItemsSource = new[] { "Casual", "Neutral", "Formal" };
            LengthDropdown.ItemsSource = new[] { "Short", "Medium", "Long" };

            LetterTypeDropdown.SelectedIndex = 0;
            ToneDropdown.SelectedIndex = 1;
            FormalityDropdown.SelectedIndex = 1;
            LengthDropdown.SelectedIndex = 1;

            if (FasterRadio != null)
                FasterRadio.Checked += (_, _) =>
                {
                    _selectedModel = Program.AppSettings.ModelSettings.Mistral.ModelName;
                    _apiUrl = Program.AppSettings.ModelSettings.Mistral.Endpoint;
                };

            if (DetailedRadio != null)
                DetailedRadio.Checked += (_, _) =>
                {
                    _selectedModel = Program.AppSettings.ModelSettings.Mixtral.ModelName;
                    _apiUrl = Program.AppSettings.ModelSettings.Mixtral.Endpoint;
                };
        }

        private async Task<string> CallLlmAsync(string prompt)
        {
            var httpClient = new HttpClient();

            var requestBody = new
            {
                model = _selectedModel,
                prompt = prompt,
                stream = true
            };

            var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
            {
                Content = JsonContent.Create(requestBody)
            };

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var fullResponse = new StringBuilder();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("response", out var responseElement))
                    {
                        fullResponse.Append(responseElement.GetString());
                    }
                }
                catch
                {
                    // Skip malformed lines silently
                }
            }

            return fullResponse.ToString();
        }

        private async void OnGenerateClick(object? sender, RoutedEventArgs e)
        {
            var letterType = LetterTypeDropdown.SelectedItem?.ToString() ?? "";
            var tone = ToneDropdown.SelectedItem?.ToString() ?? "";
            var formality = FormalityDropdown.SelectedItem?.ToString() ?? "";
            var length = LengthDropdown.SelectedItem?.ToString() ?? "";
            var userInput = UserInput.Text ?? "";

            var prompt = $"Generate a {length}, {formality}, {tone} letter of type '{letterType}'. {userInput}";

            StatusText.Text = "Generating draft...";
            PreviewBox.Text = "Generating draft...";
            var result = await CallLlmAsync(prompt);
            PreviewBox.Text = result;
            StatusText.Text = "Draft ready.";
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            var letterType = LetterTypeDropdown.SelectedItem?.ToString() ?? "Letter";
            string safeType = letterType.Replace(" ", "_").ToLower();
            string filename = $"{safeType}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            string exportPath = Program.AppSettings.Paths.ExportFolder;
            if (exportPath.StartsWith("~"))
            {
                exportPath = exportPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }

            Directory.CreateDirectory(exportPath);

            string fullPath = Path.Combine(exportPath, filename);
            await File.WriteAllTextAsync(fullPath, PreviewBox.Text);

            await PostgresHelper.InsertLetterAsync(filename, letterType, DateTime.Now, false, false);

            StatusText.Text = "Letter saved successfully.";
            await Task.Delay(3000);
            StatusText.Text = string.Empty;
        }


    }
}
