using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AIB6.Helpers
{
    public static class PromptTemplateRegistry
    {
        private static List<PromptTemplate> _templates = new();
        public static IEnumerable<PromptTemplate> GetAllTemplates()
        {
            return _templates;
        }
        public static void Load(string path)
        {
            if (path.StartsWith("~"))
                path = path.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            if (!File.Exists(path))
                throw new FileNotFoundException($"Prompt template file not found: {path}");

            string json = File.ReadAllText(path);
            _templates = JsonSerializer.Deserialize<List<PromptTemplate>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<PromptTemplate>();
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
