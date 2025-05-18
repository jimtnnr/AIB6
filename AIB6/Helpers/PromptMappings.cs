namespace AIB6.Helpers
{
    public static class PromptMappings
    {
        public static readonly Dictionary<string, string> ToneMap = new()
        {
            { "Initial Inquiry", "Maintain a polite, professional tone suitable for a first-time request or clarification." },
            { "Reminder", "Use a firm but respectful tone to follow up on a prior request or agreement." },
            { "Demand", "Write in a direct and assertive tone that communicates clear expectations without aggression." },
            { "Final Notice", "Use a serious tone that conveys urgency and imminent consequences if unresolved." },
            { "Intent to Escalate", "Adopt a formal legal tone that indicates the next step will involve legal or formal escalation." }
        };

        public static readonly Dictionary<string, string> LengthMap = new()
        {
            { "Brief", "Please keep this letter under 150 words. One short paragraph only." },
            { "Short", "Limit this letter to around 300 words. No more than two paragraphs." },
            { "Medium", "Write a letter of approximately 500 words. Include all relevant details concisely." },
            { "Extended", "Write a longer letter of about 750 words. Add supporting context and clarity." },
            { "Full", "Write a detailed and thorough letter of around 1000 words. Include all relevant background and explanation." }
        };

        public static string MapTone(string input)
        {
            return ToneMap.TryGetValue(input, out var value) ? value : input;
        }

        public static string MapLength(string input)
        {
            return LengthMap.TryGetValue(input, out var value) ? value : input;
        }
    }
}