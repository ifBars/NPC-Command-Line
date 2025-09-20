using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Tool for displaying directory structure as a tree
    /// </summary>
    public class ListTreeTool : ICodexTool
    {
        public string Name => "list_tree";

        public string Description => "Display the directory structure as a hierarchical tree. Use this to understand the overall project organization and find related files in nested directories.";

        public object ParameterSchema => new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "The relative path within the workspace to show as tree root. Use '.' for current directory",
                    @default = "."
                },
                max_depth = new
                {
                    type = "integer",
                    description = "Maximum depth of directories to traverse",
                    minimum = 1,
                    maximum = 10,
                    @default = 3
                },
                show_files = new
                {
                    type = "boolean",
                    description = "Whether to include files in the tree (directories are always shown)",
                    @default = true
                },
                exclude_patterns = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Directory/file patterns to exclude (e.g., ['bin', 'obj', '.git'])",
                    @default = new[] { "bin", "obj", ".git", ".vs", "node_modules", ".vscode" }
                },
                response_format = new
                {
                    type = "string",
                    description = "Response format: 'concise' for structure only, 'detailed' for file counts and sizes",
                    @enum = new[] { "concise", "detailed" },
                    @default = "concise"
                }
            },
            required = new string[] { }
        };

        public async Task<ToolResult> ExecuteAsync(string parameters, ToolExecutionContext context)
        {
            try
            {
                var args = JsonSerializer.Deserialize<ListTreeArgs>(parameters) ?? new ListTreeArgs();
                var fullPath = ResolvePathSafe(args.Path ?? ".", context.WorkspaceRoot);
                
                if (fullPath == null)
                {
                    return ToolResult.CreateError("Path is outside workspace boundaries");
                }

                if (!Directory.Exists(fullPath))
                {
                    return ToolResult.CreateError($"Directory not found: {args.Path}");
                }

                context.OutputAppender("\n 🌳 ", Color.DeepSkyBlue);
                context.OutputAppender($"Directory tree: {args.Path ?? "."} (max depth: {args.MaxDepth})", Color.Yellow);
                context.OutputAppender("\n", Color.White);

                var excludePatterns = new HashSet<string>(args.ExcludePatterns, StringComparer.OrdinalIgnoreCase);
                var result = new List<string>();
                var stats = new TreeStats();

                await BuildTreeAsync(fullPath, "", args, context, excludePatterns, result, stats, 0);

                var metadata = new Dictionary<string, object>
                {
                    ["root_path"] = args.Path ?? ".",
                    ["max_depth"] = args.MaxDepth,
                    ["total_directories"] = stats.DirectoryCount,
                    ["total_files"] = stats.FileCount,
                    ["excluded_items"] = stats.ExcludedCount
                };

                if (args.ResponseFormat == "detailed")
                {
                    result.Insert(0, $"Directory tree for: {args.Path ?? "."}");
                    result.Insert(1, $"Directories: {stats.DirectoryCount}, Files: {stats.FileCount}");
                    result.Insert(2, "---");
                }

                return ToolResult.CreateSuccess(string.Join("\n", result), metadata);
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"Failed to generate directory tree: {ex.Message}");
            }
        }

        private async Task BuildTreeAsync(
            string currentPath, 
            string prefix, 
            ListTreeArgs args, 
            ToolExecutionContext context,
            HashSet<string> excludePatterns,
            List<string> result,
            TreeStats stats,
            int currentDepth)
        {
            if (currentDepth >= args.MaxDepth) return;

            try
            {
                var directories = Directory.EnumerateDirectories(currentPath)
                    .Where(d => !excludePatterns.Contains(Path.GetFileName(d)))
                    .OrderBy(d => d)
                    .ToList();

                var files = args.ShowFiles 
                    ? Directory.EnumerateFiles(currentPath)
                        .Where(f => !excludePatterns.Contains(Path.GetFileName(f)))
                        .OrderBy(f => f)
                        .ToList()
                    : new List<string>();

                var totalItems = directories.Count + files.Count;
                var itemIndex = 0;

                // Process directories
                foreach (var dir in directories)
                {
                    itemIndex++;
                    var isLast = itemIndex == totalItems;
                    var dirName = Path.GetFileName(dir);
                    var connector = isLast ? "└── " : "├── ";
                    var line = $"{prefix}{connector}📁 {dirName}/";
                    
                    if (args.ResponseFormat == "detailed")
                    {
                        try
                        {
                            var itemCount = Directory.EnumerateFileSystemEntries(dir).Count();
                            line += $" ({itemCount} items)";
                        }
                        catch
                        {
                            // Ignore access errors for item counting
                        }
                    }

                    result.Add(line);
                    context.OutputAppender($" {line}\n", Color.LightBlue);
                    stats.DirectoryCount++;

                    var nextPrefix = prefix + (isLast ? "    " : "│   ");
                    await BuildTreeAsync(dir, nextPrefix, args, context, excludePatterns, result, stats, currentDepth + 1);
                }

                // Process files
                if (args.ShowFiles)
                {
                    foreach (var file in files)
                    {
                        itemIndex++;
                        var isLast = itemIndex == totalItems;
                        var fileName = Path.GetFileName(file);
                        var connector = isLast ? "└── " : "├── ";
                        var line = $"{prefix}{connector}📄 {fileName}";
                        
                        if (args.ResponseFormat == "detailed")
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);
                                var size = FormatFileSize(fileInfo.Length);
                                line += $" ({size})";
                            }
                            catch
                            {
                                // Ignore access errors for file size
                            }
                        }

                        result.Add(line);
                        context.OutputAppender($" {line}\n", Color.LightGray);
                        stats.FileCount++;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.Add($"{prefix}├── ❌ Access Denied");
                context.OutputAppender($" {prefix}├── ❌ Access Denied\n", Color.Red);
                stats.ExcludedCount++;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string? ResolvePathSafe(string relativePath, string workspaceRoot)
        {
            var combined = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
            return combined.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase) ? combined : null;
        }

        private class ListTreeArgs
        {
            [JsonPropertyName("path")]
            public string? Path { get; set; }

            [JsonPropertyName("max_depth")]
            public int MaxDepth { get; set; } = 3;

            [JsonPropertyName("show_files")]
            public bool ShowFiles { get; set; } = true;

            [JsonPropertyName("exclude_patterns")]
            public string[] ExcludePatterns { get; set; } = { "bin", "obj", ".git", ".vs", "node_modules", ".vscode" };

            [JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "concise";
        }

        private class TreeStats
        {
            public int DirectoryCount { get; set; }
            public int FileCount { get; set; }
            public int ExcludedCount { get; set; }
        }
    }
}
