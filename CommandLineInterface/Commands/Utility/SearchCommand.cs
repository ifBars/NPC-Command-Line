using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class SearchCommand : BaseCommand
    {
        public override string Name => "esearch";
        public override string Description => "Semantic search workspace using embeddinggemma";

        public override async Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var query = arguments?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                context.Append(" Usage: esearch <query>\n", Color.Yellow);
                return true;
            }

            var root = context.GetWorkspaceRoot();
            context.Append(" Building embeddings (first run may take a while) ...\n", Color.Gray);
            
            if (context.EmbeddingService.IndexedFileCount == 0)
            {
                await context.EmbeddingService.IndexWorkspaceAsync(root, progress =>
                {
                    context.Append($" {progress}\n", Color.Gray);
                });
            }
            
            var results = await context.EmbeddingService.SearchSemanticAsync(query, 10);
            foreach (var (path, similarity, snippet) in results)
            {
                context.Append($" {similarity,6:F3} {path}\n", Color.LightGray);
                if (!string.IsNullOrEmpty(snippet))
                {
                    context.Append($"   {snippet.Split('\n').FirstOrDefault()?.Trim()}\n", Color.Gray);
                }
            }
            return true;
        }
    }
}


