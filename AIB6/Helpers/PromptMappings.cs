namespace AIB6.Helpers
{
    public static class PromptMappings
    {
        public static readonly Dictionary<string, string> ToneMap = new()
        {
            { "Initial Inquiry", "a polite and professional tone" },
            { "Reminder", "a firm but courteous tone" },
            { "Demand", "a direct and assertive tone" },
            { "Final Notice", "a strong tone that conveys urgency and consequences" },
            { "Intent to Escalate", "a serious legal tone indicating imminent action" }
        };

        public static readonly Dictionary<string, string> LengthMap = new()
        {
            { "Brief", "around 150 words" },
            { "Short", "around 300 words" },
            { "Medium", "around 500 words" },
            { "Extended", "around 750 words" },
            { "Full", "around 1000 words" }
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