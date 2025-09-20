using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Tool for reading file contents from the workspace
    /// </summary>
    public class ReadFileTool : ICodexTool
    {
        public string Name => "read_file";

        public string Description => "Read the contents of a file in the workspace. Returns file content with line numbers for easy reference. Use this to examine source code, configuration files, and documentation.";

        public object ParameterSchema => new
        {
            type = "object",
            properties = new
            {
                path = new
                {
                    type = "string",
                    description = "The relative path to the file within the workspace (e.g., 'Program.cs', 'Commands/UtilityCommand.cs')"
                },
                max_lines = new
                {
                    type = "integer",
                    description = "Maximum number of lines to read from the file",
                    minimum = 1,
                    maximum = 1000,
                    @default = 200
                },
                start_line = new
                {
                    type = "integer",
                    description = "Line number to start reading from (1-based)",
                    minimum = 1,
                    @default = 1
                },
                response_format = new
                {
                    type = "string",
                    description = "Response format: 'concise' for content only, 'detailed' for metadata and line numbers",
                    @enum = new[] { "concise", "detailed" },
                    @default = "detailed"
                }
            },
            required = new[] { "path" }
        };

        public async Task<ToolResult> ExecuteAsync(string parameters, ToolExecutionContext context)
        {
            try
            {
                var args = JsonSerializer.Deserialize<ReadFileArgs>(parameters) ?? new ReadFileArgs();
                // Be lenient: if path missing but arguments look like {"file":"..."} or top-level string
                if (args.Path == null)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(parameters);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            if (doc.RootElement.TryGetProperty("file", out var fileProp) && fileProp.ValueKind == JsonValueKind.String)
                            {
                                args.Path = fileProp.GetString();
                            }
                        }
                        else if (doc.RootElement.ValueKind == JsonValueKind.String)
                        {
                            args.Path = doc.RootElement.GetString();
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(args.Path))
                {
                    return ToolResult.CreateError("File path is required");
                }

                var fullPath = ResolvePathSafe(args.Path, context.WorkspaceRoot);
                if (fullPath == null)
                {
                    return ToolResult.CreateError("Path is outside workspace boundaries");
                }

                if (!File.Exists(fullPath))
                {
                    return ToolResult.CreateError($"File not found: {args.Path}");
                }

                context.OutputAppender("\n 📄 ", Color.DeepSkyBlue);
                context.OutputAppender($"Reading file: {args.Path} (max {args.MaxLines} lines)", Color.Yellow);
                context.OutputAppender("\n", Color.White);

                var lines = await File.ReadAllLinesAsync(fullPath, context.CancellationToken);
                var startIndex = Math.Max(0, args.StartLine - 1);
                var endIndex = Math.Min(lines.Length, startIndex + args.MaxLines);
                var selectedLines = lines.Skip(startIndex).Take(endIndex - startIndex).ToList();

                var result = new List<string>();
                
                if (args.ResponseFormat == "detailed")
                {
                    result.Add($"File: {args.Path}");
                    result.Add($"Lines: {startIndex + 1}-{startIndex + selectedLines.Count} of {lines.Length}");
                    result.Add("---");
                }

                for (int i = 0; i < selectedLines.Count; i++)
                {
                    var lineNumber = startIndex + i + 1;
                    var line = selectedLines[i];
                    
                    if (args.ResponseFormat == "detailed")
                    {
                        result.Add($"{lineNumber,4}: {line}");
                        context.OutputAppender($" {lineNumber,4}: {line}\n", Color.LightGray);
                    }
                    else
                    {
                        result.Add(line);
                        context.OutputAppender($"{line}\n", Color.LightGray);
                    }
                }

                if (args.ResponseFormat == "detailed" && endIndex < lines.Length)
                {
                    result.Add($"... ({lines.Length - endIndex} more lines)");
                }

                var fileInfo = new FileInfo(fullPath);
                var metadata = new Dictionary<string, object>
                {
                    ["file_path"] = args.Path,
                    ["total_lines"] = lines.Length,
                    ["lines_read"] = selectedLines.Count,
                    ["file_size"] = fileInfo.Length,
                    ["last_modified"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return ToolResult.CreateSuccess(string.Join("\n", result), metadata);
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"Failed to read file: {ex.Message}");
            }
        }

        private static string? ResolvePathSafe(string relativePath, string workspaceRoot)
        {
            var combined = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
            return combined.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase) ? combined : null;
        }

        private class ReadFileArgs
        {
            [JsonPropertyName("path")]
            public string? Path { get; set; }

            [JsonPropertyName("max_lines")]
            public int MaxLines { get; set; } = 200;

            [JsonPropertyName("start_line")]
            public int StartLine { get; set; } = 1;

            [JsonPropertyName("response_format")]
            public string ResponseFormat { get; set; } = "detailed";
        }
    }
}
