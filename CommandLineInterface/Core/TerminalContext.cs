namespace CommandLineInterface.Core
{
    public class TerminalContext
    {
        public required Action<string, Color> Append { get; init; }
        public required Func<string> GetWorkspaceRoot { get; init; }
        public required Services.CodexService CodexService { get; init; }
        public required Services.EmbeddingService EmbeddingService { get; init; }
        public required Form1 Form { get; init; }

        public ITerminalMode? CurrentMode { get; set; }
    }
}


