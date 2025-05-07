using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Npgsql;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;

namespace AIB6;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        GenerateButton.Click += OnGenerateClick;
        SaveButton.Click += OnSaveClick; // ← this line hooks it up
    }
    private async Task<string> CallLlmAsync(string prompt)
    {
        var httpClient = new HttpClient();
        var requestBody = new
        {
            model = "mistral", // or your actual model
            prompt = prompt,
            stream = true
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate")
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
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
                    var text = responseElement.GetString();
                    fullResponse.Append(text);
                }
            }
            catch
            {
                // skip malformed chunks
            }
        }

        return fullResponse.ToString();
    }


    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        var letterType = (LetterTypeList.SelectedItem as ListBoxItem)?.Content?.ToString() ?? "";
        var tone = (ToneSelect.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var formality = (FormalitySelect.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var length = (LengthSelect.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var userInput = UserInput.Text ?? "";

        var prompt = $"Generate a {length} {tone} letter of type {letterType}. Details: {userInput}";

        PreviewBox.Text = "Generating draft...";
        var result = await CallLlmAsync(prompt);
        PreviewBox.Text = result;
    }
    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var selectedLetterType = (LetterTypeList.SelectedItem as ListBoxItem)?.Content?.ToString() ?? "Unknown";
        var timestamp = DateTime.UtcNow;
        var filename = $"{selectedLetterType}_{timestamp:MM_dd_yyyy_HHmm}";

        try
        {
            // write file
            var docsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AIBDOCS");
            Directory.CreateDirectory(docsPath);
            var filePath = Path.Combine(docsPath, filename + ".txt");
            File.WriteAllText(filePath, PreviewBox.Text);

            // write metadata
            using var conn = new NpgsqlConnection("Host=localhost;Username=postgres;Password=Kitten77;Database=AIB6");
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand("call insert_letter(@filename, @type, @ts, @fav, @hide)", conn);
            cmd.Parameters.AddWithValue("filename", filename + ".txt");
            cmd.Parameters.AddWithValue("type", selectedLetterType);
            cmd.Parameters.AddWithValue("ts", timestamp);
            cmd.Parameters.AddWithValue("fav", false);
            cmd.Parameters.AddWithValue("hide", false);

            await cmd.ExecuteNonQueryAsync();

            MessageBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandardWindow("Saved", "Draft saved successfully.")
                .Show();
        }
        catch (Exception ex)
        {
            await MessageBox.Avalonia.MessageBoxManager
                .GetMessageBoxStandardWindow("Error", ex.Message)
                .Show();
        }
    }


}