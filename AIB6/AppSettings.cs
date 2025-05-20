namespace AIB6
{
    public sealed class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public ModelSettings ModelSettings { get; set; } = new();
        public Paths Paths { get; set; } = new();
        public LLM LLM { get; set; } = new();
    }

    public class ConnectionStrings
    {
        public string Postgres { get; set; } = "";
    }

    public class ModelSettings
    {
        public string DefaultModel { get; set; } = "";
        public ModelOption Mistral { get; set; } = new();
        public ModelOption Mixtral { get; set; } = new();
    }

    public class ModelOption
    {
        public string ModelName { get; set; } = "";
        public string Endpoint { get; set; } = "";
    }

    public class Paths
    {
        public string ExportFolder { get; set; } = "";
        public string ExportUSB { get; set; } = "";
        public string ArchiveFolder { get; set; } = "";
        public string PromptTemplatesFile { get; set; } = "";
        public string ImportFolder { get; set; } = "";
        public string PromptTemplatesFolder { get; set; } = "";
    }

    public class LLM
    {
        public bool UseStreaming { get; set; }
    }
}