using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Tool for AI-powered semantic search using embeddings
    /// </summary>
    public class SemanticSearchTool : ICodexTool
    {
        public string Name => "semantic_search";

        public string Description => "Perform AI-powered semantic search to find conceptually related content in the workspace. This understands meaning and context, not just exact text matches. Use this to find code that implements similar functionality, related concepts, or answers to questions about the codebase.";

        public object ParameterSchema => new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "The semantic query describing what you're looking for (e.g., 'authentication logic', 'error handling patterns', 'database connections')"
                },
                max_results = new
                {
                    type = "integer",
                    description = "Maximum number of semantically similar results to return",
                    minimum = 1,
                    maximum = 50,
                    @default = 10
                },
                similarity_threshold = new
                {
                    type = "number",
                    description = "Minimum similarity score (0.0-1.0) for results to include",
                    minimum = 0.0,
                    maximum = 1.0,
                    @default = 0.3
                },
                response_format = new
                {
                    type = "string",
                    description = "Response format: 'concise' for file paths only, 'detailed' for snippets and context",
                    @enum = new[] { "concise", "detailed" },
                    @default = "detailed"
                }
            },
            required = new[] { "query" }
        };

        public async Task<ToolResult> ExecuteAsync(string parameters, ToolExecutionContext context)
        {
            try
            {
                var args = JsonSerializer.Deserialize<SemanticSearchArgs>(parameters);
                if (args?.Query == null)
                {
                    return ToolResult.CreateError("Search query is required");
                }

                if (context.EmbeddingService == null)
                {
                    return ToolResult.CreateError("Semantic search is not available. Embedding service not initialized.");
                }

                if (context.EmbeddingService.IndexedFileCount == 0)
                {
                    return ToolResult.CreateError("No files indexed yet. Please wait for indexing to complete or use text search instead.");
                }

                context.OutputAppender("\n 🧠 ", Color.DeepSkyBlue);
                context.OutputAppender($"Semantic search for: \"{args.Query}\" (max {args.MaxResults} results)", Color.Yellow);
                context.OutputAppender("\n", Color.White);

                var results = await context.EmbeddingService.SearchSemanticAsync(args.Query, args.MaxResults);
                
                // Filter by similarity threshold
                var filteredResults = results
                    .Where(r => r.similarity >= args.SimilarityThreshold)
                    .ToList();

                if (filteredResults.Count == 0)
                {
                    var message = results.Count > 0 
                        ? $"No results above similarity threshold {args.SimilarityThreshold:F2}. Best match was {results.First().similarity:F2}"
                        : "No semantically similar content found.";
                    
                    context.OutputAppender($" {message}\n", Color.Gray);
                    return ToolResult.CreateSuccess(message);
                }

                context.OutputAppender($" Found {filteredResults.Count} semantically relevant results:\n", Color.Cyan);

                var formattedResults = new List<string>();
                
                foreach (var (file, similarity, snippet) in filteredResults)
                {
                    if (args.ResponseFormat == "detailed")
                    {
                        formattedResults.Add($"📄 {file}");
                        formattedResults.Add($"   Similarity: {similarity:F3}");
                        
                        if (!string.IsNullOrEmpty(snippet))
                        {
                            formattedResults.Add("   Content:");
                            var lines = snippet.Split('\n');
                            var significantLines = lines
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .Take(5)
                                .ToList();
                            
                            foreach (var line in significantLines)
                            {
                                formattedResults.Add($"     {line.Trim()}");
                            }
                            
                            if (lines.Length > significantLines.Count)
                            {
                                formattedResults.Add("     ...");
                            }
                        }
                        formattedResults.Add("");
                    }
                    else
                    {
                        formattedResults.Add($"{file} (similarity: {similarity:F2})");
                    }

                    // Display in terminal
                    context.OutputAppender($" 📄 {file} (similarity: {similarity:F2})\n", Color.LightBlue);
                    if (args.ResponseFormat == "detailed" && !string.IsNullOrEmpty(snippet))
                    {
                        var lines = snippet.Split('\n');
                        foreach (var line in lines.Take(3))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                context.OutputAppender($"   {line.Trim()}\n", Color.LightGray);
                            }
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
                    ["total_matches"] = filteredResults.Count,
                    ["similarity_threshold"] = args.SimilarityThreshold,
                    ["indexed_files"] = context.EmbeddingService.IndexedFileCount,
                    ["average_similarity"] = filteredResults.Count > 0 ? filteredResults.Average(r => r.similarity) : 0,
                    ["best_similarity"] = filteredResults.Count > 0 ? filteredResults.Max(r => r.similarity) : 0
                };

                return ToolResult.CreateSuccess(string.Join("\n", formattedResults), metadata);
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"Semantic search failed: {ex.Message}");
            }
        }

        private class SemanticSearchArgs
        {
            [JsonPropertyName("query")]
            public string? Query { get; set; }

            [JsonPropertyName("max_results")]
            public int MaxResults { get; set; } = 10;

            [JsonPropertyName("similarity_threshold")]
            public float SimilarityThreshold { get; set; } = 0.3f;

            [JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "detailed";
        }
    }
}
