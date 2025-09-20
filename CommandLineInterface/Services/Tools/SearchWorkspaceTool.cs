using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Tool for searching text within workspace files
    /// </summary>
    public class SearchWorkspaceTool : ICodexTool
    {
        public string Name => "search_workspace";

        public string Description => "Search for text patterns across all files in the workspace. Supports case-insensitive text search with context lines. Use this to find specific code patterns, method names, or configuration values.";

        public object ParameterSchema => new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "The text pattern to search for in workspace files"
                },
                max_results = new
                {
                    type = "integer",
                    description = "Maximum number of search results to return",
                    minimum = 1,
                    maximum = 100,
                    @default = 20
                },
                file_extensions = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "File extensions to include in search (e.g., ['.cs', '.xml']). If empty, searches common code files",
                    @default = new string[] { }
                },
                case_sensitive = new
                {
                    type = "boolean",
                    description = "Whether the search should be case-sensitive",
                    @default = false
                },
                response_format = new
                {
                    type = "string",
                    description = "Response format: 'concise' for matches only, 'detailed' for context and metadata",
                    @enum = new[] { "concise", "detailed" },
                    @default = "concise"
                }
            },
            required = new[] { "query" }
        };

        public async Task<ToolResult> ExecuteAsync(string parameters, ToolExecutionContext context)
        {
            try
            {
                var args = JsonSerializer.Deserialize<SearchArgs>(parameters);
                if (args?.Query == null)
                {
                    return ToolResult.CreateError("Search query is required");
                }

                context.OutputAppender("\n 🔎 ", Color.DeepSkyBlue);
                context.OutputAppender($"Searching workspace for: \"{args.Query}\" (max {args.MaxResults} results)", Color.Yellow);
                context.OutputAppender("\n", Color.White);

                // Use semantic search if available and embeddings are indexed
                if (context.EmbeddingService != null && context.EmbeddingService.IndexedFileCount > 0)
                {
                    try
                    {
                        context.OutputAppender(" Using semantic search...\n", Color.Gray);
                        var semanticResults = await context.EmbeddingService.SearchSemanticAsync(args.Query, args.MaxResults);
                        return await FormatSemanticResults(semanticResults, args, context);
                    }
                    catch
                    {
                        // Fallback to text search
                        context.OutputAppender(" Semantic search failed, falling back to text search...\n", Color.Gray);
                    }
                }

                // Fallback to traditional text search
                var extensions = args.FileExtensions?.Length > 0 
                    ? new HashSet<string>(args.FileExtensions, StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs", ".xml", ".config", ".json", ".md", ".txt", ".resx", ".csproj", ".sln" };

                var results = new List<SearchResult>();
                var comparison = args.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                foreach (var file in Directory.EnumerateFiles(context.WorkspaceRoot, "*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(file))) continue;
                    if (results.Count >= args.MaxResults) break;

                    var relativePath = ToWorkspaceRelative(file, context.WorkspaceRoot);
                    var lines = await File.ReadAllLinesAsync(file, context.CancellationToken);

                    for (int i = 0; i < lines.Length && results.Count < args.MaxResults; i++)
                    {
                        if (lines[i].Contains(args.Query ?? "", comparison))
                        {
                            var result = new SearchResult
                            {
                                FilePath = relativePath,
                                LineNumber = i + 1,
                                LineContent = lines[i].Trim(),
                                ContextBefore = i > 0 ? lines[i - 1].Trim() : null,
                                ContextAfter = i < lines.Length - 1 ? lines[i + 1].Trim() : null
                            };
                            results.Add(result);

                            var displayLine = $" {relativePath}:{i + 1}: {lines[i].Trim()}";
                            context.OutputAppender($"{displayLine}\n", Color.LightGray);
                        }
                    }
                }

                if (results.Count == 0)
                {
                    context.OutputAppender(" No matches found.\n", Color.Gray);
                    return ToolResult.CreateSuccess("No matches found for the search query.");
                }

                var formattedResults = FormatTextResults(results, args);
                var metadata = new Dictionary<string, object>
                {
                    ["query"] = args.Query,
                    ["total_matches"] = results.Count,
                    ["search_type"] = "text_search",
                    ["case_sensitive"] = args.CaseSensitive
                };

                return ToolResult.CreateSuccess(formattedResults, metadata);
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"Search failed: {ex.Message}");
            }
        }

        private Task<ToolResult> FormatSemanticResults(List<(string file, float similarity, string snippet)> semanticResults, SearchArgs args, ToolExecutionContext context)
        {
            var results = new List<string>();
            
            if (semanticResults.Count == 0)
            {
                context.OutputAppender(" No semantically similar content found.\n", Color.Gray);
                return Task.FromResult(ToolResult.CreateSuccess("No semantically similar content found."));
            }

            context.OutputAppender($" Found {semanticResults.Count} semantically relevant results:\n", Color.Cyan);
            
            foreach (var (file, similarity, snippet) in semanticResults)
            {
                if (args.ResponseFormat == "detailed")
                {
                    results.Add($"📄 {file} (similarity: {similarity:F2})");
                    if (!string.IsNullOrEmpty(snippet))
                    {
                        var lines = snippet.Split('\n');
                        foreach (var line in lines.Take(3))
                        {
                            results.Add($"   {line.Trim()}");
                        }
                        if (lines.Length > 3)
                        {
                            results.Add("   ...");
                        }
                    }
                }
                else
                {
                    results.Add($"{file} (similarity: {similarity:F2})");
                }

                context.OutputAppender($" 📄 {file} (similarity: {similarity:F2})\n", Color.LightBlue);
                if (!string.IsNullOrEmpty(snippet))
                {
                    var lines = snippet.Split('\n');
                    foreach (var line in lines.Take(3))
                    {
                        context.OutputAppender($"   {line.Trim()}\n", Color.LightGray);
                    }
                    if (lines.Length > 3)
                    {
                        context.OutputAppender("   ...\n", Color.Gray);
                    }
                }
                context.OutputAppender("\n", Color.White);
            }

            var metadata = new Dictionary<string, object>
            {
                ["query"] = args.Query,
                ["total_matches"] = semanticResults.Count,
                ["search_type"] = "semantic_search"
            };

            return Task.FromResult(ToolResult.CreateSuccess(string.Join("\n", results), metadata));
        }

        private static string FormatTextResults(List<SearchResult> results, SearchArgs args)
        {
            var formatted = new List<string>();
            
            foreach (var result in results)
            {
                if (args.ResponseFormat == "detailed")
                {
                    formatted.Add($"📄 {result.FilePath}:{result.LineNumber}");
                    if (result.ContextBefore != null)
                        formatted.Add($"   - {result.ContextBefore}");
                    formatted.Add($"   > {result.LineContent}");
                    if (result.ContextAfter != null)
                        formatted.Add($"   + {result.ContextAfter}");
                    formatted.Add("");
                }
                else
                {
                    formatted.Add($"{result.FilePath}:{result.LineNumber}: {result.LineContent}");
                }
            }

            return string.Join("\n", formatted);
        }

        private static string ToWorkspaceRelative(string fullPath, string workspaceRoot)
        {
            if (fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        private class SearchArgs
        {
            [JsonPropertyName("query")]
            public string? Query { get; set; }

            [JsonPropertyName("max_results")]
            public int MaxResults { get; set; } = 20;

            [JsonPropertyName("file_extensions")]
            public string[] FileExtensions { get; set; } = Array.Empty<string>();

            [JsonPropertyName("case_sensitive")]
            public bool CaseSensitive { get; set; } = false;

            [JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "concise";
        }

        private class SearchResult
        {
            public string FilePath { get; set; } = string.Empty;
            public int LineNumber { get; set; }
            public string LineContent { get; set; } = string.Empty;
            public string? ContextBefore { get; set; }
            public string? ContextAfter { get; set; }
        }
    }
}
