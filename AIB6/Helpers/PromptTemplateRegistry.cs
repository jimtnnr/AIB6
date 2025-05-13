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

        public static List<SubTypeInfo> GetSubTypesForMainType(string mainType)
        {
            return _templates
                .Where(t => t.MainType == mainType)
                .Select(t => new SubTypeInfo { Id = t.SubType, Label = t.Label })
                .ToList();
        }

        public static PromptTemplate? GetTemplate(string mainType, string subType)
        {
            return _templates.FirstOrDefault(t => t.MainType == mainType && t.SubType == subType);
        }
    }

    public class PromptTemplate
    {
        public string MainType { get; set; } = "";
        public string SubType { get; set; } = "";
        public string Label { get; set; } = "";
        public string Structure { get; set; } = "";
        public string InputScaffold { get; set; } = "";
        public List<LengthOption> LengthOptions { get; set; } = new();
        public string PromptTemplateText { get; set; } = "";

        public string FillPrompt(string userInput, string tone, string length)
        {
            return PromptTemplateText
                .Replace("{UserInput}", userInput)
                .Replace("{Tone}", tone)
                .Replace("{Length}", length);
        }
    }

    public class LengthOption
    {
        public string Label { get; set; } = "";
        public int Words { get; set; }
    }

    public class SubTypeInfo
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
    }
}
