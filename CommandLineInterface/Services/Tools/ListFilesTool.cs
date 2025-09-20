using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Tool for listing files and directories in the workspace
    /// </summary>
    public class ListFilesTool : ICodexTool
    {
        public string Name => "list_files";

        public string Description => "List files and directories in a specified workspace path. Use this to explore the project structure and understand the codebase organization.";

        public object ParameterSchema => new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "The relative path within the workspace to list. Use '.' for current directory",
                    @default = "."
                },
                response_format = new
                {
                    type = "string",
                    description = "Response format: 'concise' for names only, 'detailed' for full information",
                    @enum = new[] { "concise", "detailed" },
                    @default = "concise"
                }
            },
            required = new string[] { }
        };

        public Task<ToolResult> ExecuteAsync(string parameters, ToolExecutionContext context)
        {
            try
            {
                var args = JsonSerializer.Deserialize<ListFilesArgs>(parameters) ?? new ListFilesArgs();
                var fullPath = ResolvePathSafe(args.Path ?? ".", context.WorkspaceRoot);
                
                if (fullPath == null)
                {
                    return Task.FromResult(ToolResult.CreateError("Path is outside workspace boundaries"));
                }

                if (!Directory.Exists(fullPath))
                {
                    return Task.FromResult(ToolResult.CreateError($"Directory not found: {args.Path}"));
                }

                context.OutputAppender("\n 🔍 ", Color.DeepSkyBlue);
                context.OutputAppender($"Listing directory: {args.Path ?? "."}", Color.Yellow);
                context.OutputAppender("\n", Color.White);

                var result = new List<string>();
                var relativePath = ToWorkspaceRelative(fullPath, context.WorkspaceRoot);
                
                result.Add($"Directory: {(string.IsNullOrEmpty(relativePath) ? "." : relativePath)}");

                var directories = Directory.EnumerateDirectories(fullPath).OrderBy(d => d).ToList();
                var files = Directory.EnumerateFiles(fullPath).OrderBy(f => f).ToList();

                foreach (var dir in directories)
                {
                    var name = Path.GetFileName(dir);
                    var line = args.ResponseFormat == "detailed" 
                        ? $"  [DIR]  {name}/ ({Directory.EnumerateFileSystemEntries(dir).Count()} items)"
                        : $"  [DIR]  {name}/";
                    result.Add(line);
                    context.OutputAppender($"  [dir]  {name}\n", Color.LightBlue);
                }

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    var line = args.ResponseFormat == "detailed"
                        ? $"  [FILE] {name} ({new FileInfo(file).Length} bytes)"
                        : $"  [FILE] {name}";
                    result.Add(line);
                    context.OutputAppender($"  [file] {name}\n", Color.LightGray);
                }

                var metadata = new Dictionary<string, object>
                {
                    ["directory_count"] = directories.Count,
                    ["file_count"] = files.Count,
                    ["path"] = args.Path ?? "."
                };

                return Task.FromResult(ToolResult.CreateSuccess(string.Join("\n", result), metadata));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.CreateError($"Failed to list directory: {ex.Message}"));
            }
        }

        private static string? ResolvePathSafe(string relativePath, string workspaceRoot)
        {
            var combined = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
            return combined.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase) ? combined : null;
        }

        private static string ToWorkspaceRelative(string fullPath, string workspaceRoot)
        {
            if (fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        private class ListFilesArgs
        {
            [JsonPropertyName("path")]
            public string? Path { get; set; }

            [JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "concise";
        }
    }
}
