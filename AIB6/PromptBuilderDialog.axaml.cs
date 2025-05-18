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
            string to = ToField.Text?.Trim() ?? "";
            string from = FromField.Text?.Trim() ?? "";
            string where = WhereField.Text?.Trim() ?? "";
            string whereWhen = WhereWhenField.Text?.Trim() ?? "";
            string want = WantField.Text?.Trim() ?? "";

            AdditionalInfo = "";

            if (!string.IsNullOrEmpty(from))
                AdditionalInfo += $"From: {from}\n";

            if (!string.IsNullOrEmpty(to))
                AdditionalInfo += $"To: {to}\n";

            if (!string.IsNullOrEmpty(where))
                AdditionalInfo += $"\nWhere: {where}\n";

            if (!string.IsNullOrEmpty(whereWhen))
                AdditionalInfo += $"\nWhat happened:\n{whereWhen}\n";

            if (!string.IsNullOrEmpty(want))
                AdditionalInfo += $"\nWhat I want:\n{want}\n";

            Close(true);
        }

        private void OnCancel(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}