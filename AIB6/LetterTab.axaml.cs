using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AIB6;
using AIB6.Helpers;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AIB6
{
    public partial class LetterTab : UserControl
    {
        private string _selectedModel;
        private string _apiUrl;
        private bool _letterGenerated = false;
        private string? _lastPromptToName = null;

        public LetterTab()
        {
            InitializeComponent();
            VoiceRecorder.TranscriptReady += (_, transcript) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        var clean = PromptSanitizer.Clean(transcript);
                        if (string.IsNullOrWhiteSpace(UserInput.Text))
                        {
                            UserInput.Text = clean;
                        }
                        else
                        {
                            UserInput.Text = UserInput.Text.TrimEnd() + "\n\n" + clean;
                        }
                    }
                });
            };

            VoiceRecorder.ClearPressed += (_, _) =>
            {
                UserInput.Text = ""; // or however you want to respond
                StatusText.Text = "Cleared via voice recorder";
            };

            SaveButton.IsEnabled = false;
            GenerateButton.IsEnabled = true;
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
         
            LetterTypeDropdown.ItemsSource = PromptTemplateRegistry.GetMainTypeDisplayNames();

            if (LetterTypeDropdown.Items.Count > 0)
                LetterTypeDropdown.SelectedIndex = 0;
            OnLetterTypeChanged(LetterTypeDropdown, null);

            //Escalation
            ToneDropdown.ItemsSource = new[]
            {
                "Initial Inquiry",
                "Reminder",
                "Demand",
                "Final Notice",
                "Intent to Escalate"
            };
            ToneDropdown.SelectedIndex = 0;

            LengthDropdown.ItemsSource = new[]
            {
                "Brief (~150 words)",
                "Short (~300 words)",
                "Medium (~500 words)",
                "Extended (~750 words)",
                "Full (~1000 words)"
            };


            LetterTypeDropdown.SelectionChanged += OnLetterTypeChanged;
            SubtypeDropdown.SelectionChanged += OnSubTypeChanged;

            ToneDropdown.SelectedIndex = 1;
            LengthDropdown.SelectedIndex = 2;

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
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private async void OnPromptBuilderClick(object? sender, RoutedEventArgs e)
        {
            var dialog = new PromptBuilderDialog();
            var result = await dialog.ShowDialog<bool>(this.GetVisualRoot() as Window);

            if (result && !string.IsNullOrWhiteSpace(dialog.AdditionalInfo))
            {
                var clean = PromptSanitizer.Clean(dialog.AdditionalInfo);
                if (string.IsNullOrWhiteSpace(UserInput.Text))
                {
                    UserInput.Text = clean;
                }
                else
                {
                    UserInput.Text = UserInput.Text.TrimEnd() + "\n\n" + clean;
                }
            }

        }
        private string Slugify(string input)
        {
            var slug = Regex.Replace(input.ToLower(), @"[^a-z0-9]+", "_").Trim('_');
            return slug;
        }

        private async Task<string> CallLlmAsync(string prompt)
        {
            Console.WriteLine($"[DEBUG PROMPT]\n{prompt}");

            try
            {
               
                var httpClient = _httpClient;
                var requestBody = new
                {
                    model = _selectedModel,
                    prompt = prompt,
                    stream = true
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8,
                        "application/json")

                };

               // var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
               // response.EnsureSuccessStatusCode();
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                
                using var reader = new StreamReader(stream);

                var fullResponse = new StringBuilder();

                while (!reader.EndOfStream)
                {
                   // var line = await reader.ReadLineAsync();
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("response", out var responseElement))
                        {
                            fullResponse.Append(responseElement.GetString());
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"[PARSE ERROR]: {parseEx.Message}");
                    }
                }

                var result = fullResponse.ToString();
                if (string.IsNullOrWhiteSpace(result))
                    Console.WriteLine("[WARNING]: Model returned an empty response.");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LLM ERROR]: {ex.Message}");
                return "[Error calling language model: " + ex.Message + "]";
            }
        }


private async void OnGenerateClick(object? sender, RoutedEventArgs e)
{
    //var mainType = LetterTypeDropdown.SelectedItem?.ToString() ?? "";
    var mainType = LetterTypeDropdown.SelectedItem?.ToString()?.Split('>')?.Last().Trim() ?? "";


    var subTypeLabel = SubtypeDropdown.SelectedItem?.ToString() ?? "";
    var toneLabel = ToneDropdown.SelectedItem?.ToString() ?? "";
    var rawLength = LengthDropdown.SelectedItem?.ToString() ?? "";
    var lengthLabel = rawLength.Split('(')[0].Trim();

    var tone = PromptMappings.MapTone(toneLabel);
    var length = PromptMappings.MapLength(lengthLabel);
    var userInput = UserInput.Text ?? "";

    var templateInfo = PromptTemplateRegistry.GetSubTypesForMainType(mainType)
        .FirstOrDefault(t => t.Label == subTypeLabel);

    if (templateInfo == null)
    {
        StatusText.Text = "Template not found.";
        return;
    }

    var fullTemplate = PromptTemplateRegistry.GetTemplate(mainType, templateInfo.Id);
    if (fullTemplate == null)
    {
        StatusText.Text = "Prompt template unavailable.";
        return;
    }

    var prompt = fullTemplate
        .FillPrompt(userInput, tone, length, mainType, templateInfo.Id)
        .Replace("{AppTitle}", fullTemplate.Title);
    Console.Write(prompt);

    LetterTypeDropdown.SelectedItem = LetterTypeDropdown.SelectedItem;
    SubtypeDropdown.SelectedItem = SubtypeDropdown.SelectedItem;
    ToneDropdown.SelectedItem = ToneDropdown.SelectedItem;
    LengthDropdown.SelectedItem = LengthDropdown.SelectedItem;

    GenerateButton.IsEnabled = false;
    SaveButton.IsEnabled = false;
    PreviewBox.Text = "Generating draft...";

    var stopwatch = Stopwatch.StartNew();
    var cancel = false;

// UI-safe live timer
    _ = Task.Run(async () =>
    {
        while (!cancel)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var elapsed = stopwatch.Elapsed;
                var minutes = elapsed.Minutes;
                var seconds = elapsed.Seconds;
                StatusText.Text = $"Generating draft... ({minutes:D2}:{seconds:D2})";

            });
            await Task.Delay(1000);
        }
    });


    //var result = await CallLlmAsync(prompt);
    var result = await Task.Run(() => CallLlmAsync(prompt));

    stopwatch.Stop();
    cancel = true;

    PreviewBox.Text = result;
    var elapsed = stopwatch.Elapsed;
    StatusText.Text = $"Draft ready. ({elapsed.Minutes:D2}:{elapsed.Seconds:D2})";
    _letterGenerated = true;
    SaveButton.IsEnabled = true;
    GenerateButton.IsEnabled = false;
}


            private string GetLengthCode(string lengthLabel)
            {
                if (lengthLabel.ToLower().Contains("brief") || lengthLabel.ToLower().Contains("short"))
                    return "short";
                if (lengthLabel.ToLower().Contains("long"))
                    return "long";
                return "med";
            }
            private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
          

            var letterType = LetterTypeDropdown.SelectedItem?.ToString() ?? "Letter";
            var parts = letterType.Split('>');
            var rawTitle = parts[0].Trim().ToLower().Replace(" ", "_");
            var rawMainType = parts.Length > 1 ? parts[1].Trim().ToLower().Replace(" ", "_") : "unknown";

            string type = CleanLabel(LetterTypeDropdown.SelectedItem?.ToString()?.Split('>')?.Last() ?? "Letter");
            string subtype = CleanLabel(SubtypeDropdown.SelectedItem?.ToString() ?? "General");
            string intent = CleanLabel(ToneDropdown.SelectedItem?.ToString() ?? "Neutral");
            string length = CleanLengthLabel(LengthDropdown.SelectedItem?.ToString() ?? "Medium");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string filename = $"{type}_{subtype}_{intent}_{length}_{timestamp}.txt";


            if (!_letterGenerated)
            {
                StatusText.Text = "Please generate a letter first.";
                await Task.Delay(3000);
                StatusText.Text = string.Empty;
                return;
            }
            SaveButton.IsEnabled = false;
            _letterGenerated = false;
            string exportPath = Program.AppSettings.Paths.ExportFolder;
            if (exportPath.StartsWith("~"))
            {
                exportPath = exportPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }

            Directory.CreateDirectory(exportPath);

            string fullPath = Path.Combine(exportPath, filename);
            await File.WriteAllTextAsync(fullPath, PreviewBox.Text);

            await PostgresHelper.InsertLetterAsync(filename, letterType, DateTime.Now, false, false);
 
            StatusText.Text = string.Empty;
            StatusText.Text = "Saving Draft....";
            SaveButton.IsEnabled = false;
            //PreviewBox.Text = "Saving Draft.";
            await Task.Delay(250);
            //StatusText.Text = "Draft Saved Successfully.";
            PreviewBox.Text = "Draft Saved Successfully. Ready For Next Draft.";
            //StatusText.Text = string.Empty;
            StatusText.Text = string.Empty;
            GenerateButton.IsEnabled = true;

// Reassert user input (optional)
            UserInput.Text = UserInput.Text;
        }
        private string CleanLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
            return string.Concat(raw
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpper(word[0]) + word.Substring(1)));
        }

        private string CleanLengthLabel(string raw)
        {
            if (raw.ToLower().Contains("brief")) return "Short";
            if (raw.ToLower().Contains("long")) return "Long";
            return "Medium";
        }
        private void OnLetterTypeChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedText = LetterTypeDropdown.SelectedItem?.ToString();
            var selectedMainType = selectedText?.Split('>')?.Last().Trim();
            var selectedTitle = selectedText?.Split('>')?.First().Trim();


            if (string.IsNullOrWhiteSpace(selectedMainType)) return;

            var subTypes = PromptTemplateRegistry.GetSubTypesForTitleAndMainType(selectedTitle ?? "", selectedMainType ?? "");

            var subTypeLabels = subTypes.Select(s => s.Label).ToList();

            SubtypeDropdown.ItemsSource = subTypeLabels;

            if (subTypeLabels.Count > 0)
                SubtypeDropdown.SelectedIndex = 0;

            // Load first subType's scaffold as watermark
            var selectedSub = subTypes.FirstOrDefault();
            if (selectedSub != null)
            {
                var template = PromptTemplateRegistry.GetTemplate(selectedMainType, selectedSub.Id);
                if (template != null)
                    UserInput.Watermark = template.InputScaffold;
            }
        }
        private void OnSubTypeChanged(object? sender, SelectionChangedEventArgs e)
        {
            var mainType = LetterTypeDropdown.SelectedItem?.ToString()?.Split('>')?.Last().Trim();
            if (string.IsNullOrWhiteSpace(mainType)) return;

            var subTypeLabel = SubtypeDropdown.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(mainType) || string.IsNullOrWhiteSpace(subTypeLabel))
                return;

            var match = PromptTemplateRegistry.GetSubTypesForMainType(mainType)
                .FirstOrDefault(x => x.Label == subTypeLabel);

            if (match != null)
            {
                var template = PromptTemplateRegistry.GetTemplate(mainType, match.Id);
                if (template != null)
                    UserInput.Watermark = template.InputScaffold;
            }
        }




    }
}
