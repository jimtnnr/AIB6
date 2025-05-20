using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AIB6;

namespace AIB6.Helpers
{
    public static class PromptTemplateRegistry
    {
        private static List<PromptTemplate> _templates = new();
        public static IEnumerable<PromptTemplate> GetAllTemplates()
        {
            return _templates;
        }
        public static List<string> GetMainTypeDisplayNames()
        {
            return _templates
                .Select(t => $"{t.Title} > {t.MainType}")
                .Distinct()
                .ToList();
        }
        public static List<PromptTemplate.SubTypeInfo> GetSubTypesForTitleAndMainType(string title, string mainType)
        {
            return _templates
                .Where(t => t.Title == title && t.MainType == mainType)
                .Select(t => new PromptTemplate.SubTypeInfo { Id = t.SubType, Label = t.Label })
                .ToList();
        }

        public static void Load(string folderPath)
        {
            if (folderPath.StartsWith("~"))
                folderPath = folderPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Template folder not found: {folderPath}");

            var newTemplates = new List<PromptTemplate>();

            var files = Directory.GetFiles(folderPath, "*.aibcodex", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var templates = JsonSerializer.Deserialize<List<PromptTemplate>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (templates == null || templates.Count == 0)
                    {
                        Console.WriteLine($"[Template Skipped] {file}: No valid templates found.");
                        continue;
                    }

                    newTemplates.AddRange(templates);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Template Load Error] {file}: {ex.Message}");
                }
            }

            _templates = newTemplates;
        }

        public static List<string> GetAllMainTypes()
        {
            return _templates.Select(t => t.MainType).Distinct().ToList();
        }

        public static List<PromptTemplate.SubTypeInfo> GetSubTypesForMainType(string mainType)
        {
            return _templates
                .Where(t => t.MainType == mainType)
                .Select(t => new PromptTemplate.SubTypeInfo { Id = t.SubType, Label = t.Label })
                .ToList();
        }

        public static PromptTemplate? GetTemplate(string mainType, string subType)
        {
            return _templates.FirstOrDefault(t => t.MainType == mainType && t.SubType == subType);
        }
    }

    public class PromptTemplate
    {
        public string Title { get; set; } = string.Empty;
        public string MainType { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Structure { get; set; } = string.Empty;
        public string Intent { get; set; } = string.Empty;
        public string InputScaffold { get; set; } = string.Empty;
        public List<LengthOption> LengthOptions { get; set; } = new();
        public Dictionary<string, string> ToneDirectives { get; set; } = new();
        public string PromptTemplateText { get; set; } = string.Empty;
        public string RoleInstruction { get; set; } = string.Empty;

        public string FillPrompt(string userInput, string toneLabel, string length, string mainType, string subType)
        {
            var toneDirective = ToneDirectives.TryGetValue(toneLabel, out var result) ? result : toneLabel;

            var facts = string.IsNullOrWhiteSpace(userInput)
                ? "[Who] was involved\n[What] happened\n[When] it occurred\n[Where] it occurred\n[Why] this letter is being sent"
                : userInput;

            var role = string.IsNullOrWhiteSpace(RoleInstruction)
                ? "You are a professional preparing a formal letter."
                : RoleInstruction;

            return PromptTemplateText
                .Replace("{UserInput}", facts)
                .Replace("{Tone}", toneDirective)
                .Replace("{Length}", length)
                .Replace("{Structure}", Structure)
                .Replace("{Intent}", Intent)
                .Replace("{MainType}", mainType)
                .Replace("{SubType}", subType)
                .Replace("{Role}", role);
        }

        public class LengthOption
        {
            public string Label { get; set; } = string.Empty;
            public int Words { get; set; }
        }

        public class SubTypeInfo
        {
            public string Id { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
        }
    }
}
