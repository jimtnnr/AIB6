using System.Text.RegularExpressions;

namespace AIB6.Helpers
{
    public static class PromptSanitizer
    {
        private const int MaxLength = 1000;

        public static string Clean(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "[input missing]";

            var safe = input
                .Replace("“", "\"").Replace("”", "\"")
                .Replace("‘", "'").Replace("’", "'")
                .Replace("—", "-").Replace("–", "-")
                .Replace("\r\n", "\n").Replace("\r", "\n")
                .Replace("\\", "").Replace("/", "")
                .Replace("\"", "'")
                .Replace("\u00A0", " ");

            // Remove control characters (non-printable ASCII), but preserve \n (0x0A) and \t (0x09)
            safe = Regex.Replace(safe, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

            safe = safe.Trim();

            return safe.Length > MaxLength
                ? safe.Substring(0, MaxLength) + "…"
                : safe;
        }
    }
}