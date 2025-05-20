using AIB6.Helpers;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIB6
{
    public partial class PromptBuilderDialog : Window
    {
        public string AdditionalInfo { get; private set; } = string.Empty;

        public PromptBuilderDialog()
        {
            InitializeComponent();
        }

        private void OnInsert(object? sender, RoutedEventArgs e)
        {
            string to = PromptSanitizer.Clean(ToField?.Text ?? "");
            string from = PromptSanitizer.Clean(FromField?.Text ?? "");
            string where = PromptSanitizer.Clean(WhereField?.Text ?? "");
            string whereWhen = PromptSanitizer.Clean(WhereWhenField?.Text ?? "");
            string want = PromptSanitizer.Clean(WantField?.Text ?? "");


            AdditionalInfo = "";

            if (!string.IsNullOrEmpty(from))
                AdditionalInfo += $"From: {from}\n";

            if (!string.IsNullOrEmpty(to))
                AdditionalInfo += $"To: {to}\n";

            if (!string.IsNullOrEmpty(where))
                AdditionalInfo += $"Where: {where}\n";

            if (!string.IsNullOrEmpty(whereWhen))
                AdditionalInfo += $"What happened:\n{whereWhen}\n";

            if (!string.IsNullOrEmpty(want))
                AdditionalInfo += $"What I want:\n{want}\n";


            Close(true);
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}