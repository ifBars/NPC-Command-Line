using System.Text;
using System.Text.RegularExpressions;

namespace CommandLineInterface.Services
{
    public class CodexSession
    {
        private readonly CodexService codexService;
        private readonly EmbeddingIndexService embeddingIndex;
        private readonly Action<string, Color> append;
        private readonly List<(string role, string content)> conversation = new List<(string role, string content)>();

        public string WorkspaceRoot { get; }
        public bool IsActive { get; private set; }

        public CodexSession(CodexService codexService, EmbeddingIndexService embeddingIndex, Action<string, Color> appendOutput, string workspaceRoot)
        {
            this.codexService = codexService;
            this.embeddingIndex = embeddingIndex;
            append = appendOutput;
            WorkspaceRoot = workspaceRoot;
        }

        public async Task StartAsync()
        {
            if (IsActive) return;
            
            // Test connection first
            if (!await codexService.TestConnectionAsync())
            {
                append(" CODEX ", Color.DeepSkyBlue);
                append("Error: Cannot connect to Ollama. Make sure it's running on localhost:11434\n", Color.Red);
                return;
            }

            IsActive = true;
            append("\n CODEX ", Color.DeepSkyBlue);
            append($"interactive mode enabled. Current model: {codexService.CurrentModel}\n", Color.White);
            append(" Commands: /help, /models, /model <name>, /pull <name>, /embed <cmd>, /exit\n", Color.Gray);
            append(" Tools: ls, open, search, vsearch (use with function calls)\n\n", Color.Gray);

            // Initialize conversation with system message
            conversation.Clear();
            conversation.Add(("system", @"You are Codex, a helpful C# coding assistant working in a terminal environment. 
You have access to workspace tools through function calls. Always use tools when users ask about files or code.

Available tools:
- list_files(path=""."") - List files and directories  
- read_file(path, max_lines=200) - Read file contents
- search_workspace(query, max_results=20) - Search text in workspace files

Use JSON function calls like: {""function"": ""read_file"", ""arguments"": {""path"": ""Form1.cs"", ""max_lines"": 50}}

Keep responses concise and actionable. Show code examples when helpful."));
        }

        public void Stop()
        {
            if (!IsActive) return;
            IsActive = false;
            append("\n CODEX ", Color.DeepSkyBlue);
            append("mode exited.\n", Color.White);
        }

        public async Task HandleUserInputAsync(string input, CancellationToken cancellationToken)
        {
            if (!IsActive)
            {
                append(" CODEX mode is not active.\n", Color.Yellow);
                return;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            var normalized = input.Trim();
            if (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                append($" [DEBUG] Handling management command: {normalized}\n", Color.Gray);
                await HandleManagementCommandAsync(normalized, cancellationToken);
                return;
            }

            // Handle implicit workspace tool requests without slash or with natural phrasing
            if (TryHandleImplicitToolCommand(normalized) || TryHandleNaturalWorkspaceAsk(normalized))
            {
                return;
            }

            // Add user message to conversation
            conversation.Add(("user", input));

            // Keep conversation manageable (last 8 messages + system)
            if (conversation.Count > 9)
            {
                var systemMsg = conversation.First();
                var recent = conversation.Skip(conversation.Count - 8).ToList();
                conversation.Clear();
                conversation.Add(systemMsg);
                conversation.AddRange(recent);
            }

            // Build a simple prompt for the generate endpoint
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("System: You are Codex, a helpful C# coding assistant working in a terminal environment.");
            promptBuilder.AppendLine("System: You have access to workspace tools through function calls. When a user asks about files or code, emit JSON function calls as needed.");
            promptBuilder.AppendLine("System: Available tools => list_files(path) | read_file(path, max_lines) | search_workspace(query, max_results) | vector_search(query, top_k)");
            promptBuilder.AppendLine("System: Use JSON like {\"function\":\"read_file\",\"arguments\":{\"path\":\"Form1.cs\",\"max_lines\":50}} when calling tools. For semantic search: {\"function\":\"vector_search\",\"arguments\":{\"query\":\"http handler\",\"top_k\":8}}");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine($"User: {input}");
            promptBuilder.Append("Assistant:");

            var assistantResponse = new StringBuilder();
            try
            {
                await codexService.StreamResponseAsync(promptBuilder.ToString(), chunk =>
                {
                    assistantResponse.Append(chunk);
                    append(chunk, Color.LightGray);
                }, cancellationToken);

                // Process any function calls in the response
                var fullResponse = assistantResponse.ToString();
                await ProcessFunctionCallsAsync(fullResponse);

                // Add assistant response to conversation
                if (assistantResponse.Length > 0)
                {
                    conversation.Add(("assistant", fullResponse));
                }
                else
                {
                    append(" [No response from model. Try /models or switch with /model llama3:8b]\n", Color.Yellow);
                }
            }
            catch (Exception ex)
            {
                append($" Error: {ex.Message}\n", Color.Red);
            }
        }

        private async Task HandleManagementCommandAsync(string input, CancellationToken cancellationToken)
        {
            var parts = SplitArgs(input);
            var cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/help":
                    append(" Management commands:\n", Color.White);
                    append("  /models              List available models\n", Color.Gray);
                    append("  /model <name>        Switch to a different model\n", Color.Gray);
                    append("  /pull <name>         Download a new model\n", Color.Gray);
                    append("  /embed build         Build semantic index with embeddinggemma\n", Color.Gray);
                    append("  /embed search <q>    Search semantic index (top 8)\n", Color.Gray);
                    append("  /embed stats         Show index info\n", Color.Gray);
                    append("  /embed clear         Remove index\n", Color.Gray);
                    append("  /exit                Exit codex mode\n", Color.Gray);
                    append("\n Ask me to use workspace tools like 'list files' or 'show Form1.cs'\n", Color.LightBlue);
                    return;

                case "/exit":
                    Stop();
                    return;

                case "/models":
                    await ListModelsAsync();
                    return;

                case "/model":
                    if (parts.Count > 1)
                        await SwitchModelAsync(parts[1]);
                    else
                        append(" Usage: /model <name>\n", Color.Yellow);
                    return;

                case "/pull":
                    if (parts.Count > 1)
                        await PullModelAsync(parts[1]);
                    else
                        append(" Usage: /pull <model-name>\n", Color.Yellow);
                    return;

                case "/embed":
                    await HandleEmbedCommandAsync(parts.Skip(1).ToList(), cancellationToken);
                    return;

                default:
                    append(" Unknown command. Use /help.\n", Color.Yellow);
                    return;
            }
        }

        private async Task HandleEmbedCommandAsync(List<string> args, CancellationToken cancellationToken)
        {
            if (args.Count == 0)
            {
                append(" Usage: /embed <build|search|stats|clear> [args]\n", Color.Yellow);
                return;
            }
            var sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "build":
                    append(" [index] building semantic index with embeddinggemma:latest...\n", Color.Cyan);
                    append(" Tip: If this fails, run /pull embeddinggemma:latest first.\n", Color.Gray);
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        await embeddingIndex.BuildIndexAsync(append, cts.Token);
                    }
                    return;
                case "search":
                    if (args.Count < 2)
                    {
                        append(" Usage: /embed search <query> [--k N]\n", Color.Yellow);
                        return;
                    }
                    int topK = 8;
                    for (int i = 2; i < args.Count - 1; i++)
                    {
                        if (args[i] == "--k" && int.TryParse(args[i + 1], out var k))
                        {
                            topK = Math.Clamp(k, 1, 50);
                        }
                    }
                    await RunVectorSearchAsync(string.Join(" ", args.Skip(1)), topK, cancellationToken);
                    return;
                case "stats":
                    var (chunks, bytes) = embeddingIndex.Stats();
                    append($" [index] chunks: {chunks}, size: {bytes} bytes\n", Color.LightGray);
                    return;
                case "clear":
                    embeddingIndex.Clear();
                    append(" [index] cleared\n", Color.LightGreen);
                    return;
                default:
                    append(" Usage: /embed <build|search|stats|clear> [args]\n", Color.Yellow);
                    return;
            }
        }

        private async Task ListModelsAsync()
        {
            append(" [DEBUG] ListModelsAsync called\n", Color.Gray);
            append(" Available models:\n", Color.White);
            var models = await codexService.GetAvailableModelsAsync();
            append($" [DEBUG] Found {models.Count} models\n", Color.Gray);
            foreach (var model in models)
            {
                var indicator = model.Name == codexService.CurrentModel ? " *" : "  ";
                append($"{indicator} {model.Name}\n", model.Name == codexService.CurrentModel ? Color.LightGreen : Color.Gray);
            }
            if (models.Count == 0)
            {
                append("  No models found. Use /pull to download a model.\n", Color.Yellow);
            }
        }

        private async Task SwitchModelAsync(string modelName)
        {
            append($" Switching to model: {modelName}...\n", Color.Cyan);
            if (await codexService.SwitchModelAsync(modelName))
            {
                append($" Successfully switched to {modelName}\n", Color.LightGreen);
                // Clear conversation history when switching models
                /*
                conversation.Clear();
                conversation.Add(("system", @"You are Codex, a helpful C# coding assistant working in a terminal environment. 
You have access to workspace tools through function calls. Always use tools when users ask about files or code.

Available tools:
- list_files(path=""."") - List files and directories  
- read_file(path, max_lines=200) - Read file contents
- search_workspace(query, max_results=20) - Search text in workspace files

Use JSON function calls like: {""function"": ""read_file"", ""arguments"": {""path"": ""Form1.cs"", ""max_lines"": 50}}

Keep responses concise and actionable. Show code examples when helpful."));
                */
            }
            else
            {
                append($" Failed to switch to {modelName}. Model may not be available.\n", Color.Red);
            }
        }

        private async Task PullModelAsync(string modelName)
        {
            append($" Pulling model: {modelName}...\n", Color.Cyan);
            var result = await codexService.PullModelAsync(modelName, progress => append(progress, Color.Gray));
            if (result == "Success")
            {
                append($" Successfully pulled {modelName}\n", Color.LightGreen);
            }
            else
            {
                append($" {result}\n", Color.Red);
            }
        }

        private async Task ProcessFunctionCallsAsync(string response)
        {
            try
            {
                // Find JSON blocks containing function and arguments anywhere in the response
                var pattern = "\\{[\\s\\S]*?\\\"function\\\"[\\s\\S]*?\\\"arguments\\\"[\\s\\S]*?\\}";
                var matches = Regex.Matches(response, pattern, RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    var json = match.Value.Trim();
                    await ExecuteFunctionCallAsync(json);
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        private bool TryHandleImplicitToolCommand(string input)
        {
            // list files [path]
            var list = Regex.Match(input, "^(list files)(?:\\s+(?<p>.+))?$", RegexOptions.IgnoreCase);
            if (list.Success)
            {
                var p = list.Groups["p"].Success ? list.Groups["p"].Value : ".";
                ListDirectory(p);
                return true;
            }

            // open <path> [--head N]
            var open = Regex.Match(input, "^open\\s+(?<path>.+?)(?:\\s+--head\\s+(?<n>\\d+))?$", RegexOptions.IgnoreCase);
            if (open.Success)
            {
                var args = new List<string> { open.Groups["path"].Value };
                if (open.Groups["n"].Success)
                {
                    args.Add("--head");
                    args.Add(open.Groups["n"].Value);
                }
                OpenFile(args.ToArray());
                return true;
            }

            // search <text> [--max N]
            var search = Regex.Match(input, "^search\\s+(?<q>.+?)(?:\\s+--max\\s+(?<m>\\d+))?$", RegexOptions.IgnoreCase);
            if (search.Success)
            {
                var args = new List<string> { search.Groups["q"].Value };
                if (search.Groups["m"].Success)
                {
                    args.Add("--max");
                    args.Add(search.Groups["m"].Value);
                }
                SearchWorkspace(args.ToArray());
                return true;
            }

            // vsearch <text> [--k N]
            var vsearch = Regex.Match(input, "^vsearch\\s+(?<q>.+?)(?:\\s+--k\\s+(?<k>\\d+))?$", RegexOptions.IgnoreCase);
            if (vsearch.Success)
            {
                var query = vsearch.Groups["q"].Value;
                int topK = 8;
                if (vsearch.Groups["k"].Success && int.TryParse(vsearch.Groups["k"].Value, out var k))
                {
                    topK = Math.Clamp(k, 1, 50);
                }
                _ = RunVectorSearchAsync(query, topK, CancellationToken.None);
                return true;
            }

            return false;
        }

        private bool TryHandleNaturalWorkspaceAsk(string input)
        {
            var text = input.ToLowerInvariant();
            if (text.Contains("workspace") || text.Contains("project") || text.Contains("solution"))
            {
                ListDirectory(".");
                return true;
            }
            return false;
        }

        private async Task ExecuteFunctionCallAsync(string jsonCall)
        {
            try
            {
                // Simple JSON parsing for function calls
                if (jsonCall.Contains("\"list_files\""))
                {
                    var path = ExtractArgument(jsonCall, "path") ?? ".";
                    ListDirectory(path);
                }
                else if (jsonCall.Contains("\"read_file\""))
                {
                    var path = ExtractArgument(jsonCall, "path");
                    var maxLines = int.TryParse(ExtractArgument(jsonCall, "max_lines"), out var ml) ? ml : 200;
                    if (path != null)
                        OpenFile(new[] { path, "--head", maxLines.ToString() });
                }
                else if (jsonCall.Contains("\"search_workspace\""))
                {
                    var query = ExtractArgument(jsonCall, "query");
                    var maxResults = int.TryParse(ExtractArgument(jsonCall, "max_results"), out var mr) ? mr : 20;
                    if (query != null)
                        SearchWorkspace(new[] { query, "--max", maxResults.ToString() });
                }
                else if (jsonCall.Contains("\"vector_search\""))
                {
                    var query = ExtractArgument(jsonCall, "query");
                    var topK = int.TryParse(ExtractArgument(jsonCall, "top_k"), out var tk) ? tk : 8;
                    if (!string.IsNullOrEmpty(query))
                        await RunVectorSearchAsync(query!, Math.Clamp(topK, 1, 50), CancellationToken.None);
                }
            }
            catch
            {
                // Ignore function call errors
            }
        }

        private async Task RunVectorSearchAsync(string query, int topK, CancellationToken token)
        {
            if (embeddingIndex.Count == 0)
            {
                append(" [index] empty. Run /embed build first.\n", Color.Yellow);
                return;
            }
            var results = await embeddingIndex.SearchAsync(query, topK, token);
            if (results.Count == 0)
            {
                append(" No semantic matches found.\n", Color.Gray);
                return;
            }
            append($" Semantic results for: '{query}'\n", Color.Cyan);
            foreach (var r in results)
            {
                append($"  {r.FilePath}:{r.StartLine}  score={r.Score:F3}\n", Color.LightGray);
                append($"   {r.Preview}\n", Color.Gray);
            }
        }

        private string? ExtractArgument(string json, string argName)
        {
            var pattern = $"\"{argName}\"\\s*:\\s*\"([^\"]+)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private void ListDirectory(string? relative)
        {
            var full = ResolvePathSafe(relative ?? ".");
            if (full == null)
            {
                append(" Path outside workspace is not allowed.\n", Color.Yellow);
                return;
            }
            if (!Directory.Exists(full))
            {
                append(" Directory not found.\n", Color.Yellow);
                return;
            }

            var header = ToWorkspaceRelative(full);
            if (string.IsNullOrEmpty(header)) header = ".";
            append($" Directory: {header}\n", Color.Cyan);
            foreach (var dir in Directory.EnumerateDirectories(full).OrderBy(d => d))
            {
                append($"  [dir]  {Path.GetFileName(dir)}\n", Color.LightBlue);
            }
            foreach (var file in Directory.EnumerateFiles(full).OrderBy(f => f))
            {
                append($"  [file] {Path.GetFileName(file)}\n", Color.LightGray);
            }
        }

        private void OpenFile(string[] args)
        {
            if (args.Length == 0)
            {
                append(" Usage: /open <path> [--head N]\n", Color.Yellow);
                return;
            }

            var path = args[0];
            int head = 200;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--head" && int.TryParse(args[i + 1], out var n))
                {
                    head = Math.Clamp(n, 1, 1000);
                }
            }

            var full = ResolvePathSafe(path);
            if (full == null)
            {
                append(" Path outside workspace is not allowed.\n", Color.Yellow);
                return;
            }
            if (!File.Exists(full))
            {
                append(" File not found.\n", Color.Yellow);
                return;
            }

            append($" \n{ToWorkspaceRelative(full)}\n", Color.Cyan);
            int lineNo = 1;
            foreach (var line in File.ReadLines(full).Take(head))
            {
                append($" {lineNo,4}: {line}\n", Color.LightGray);
                lineNo++;
            }
            append("\n", Color.LightGray);
        }

        private void SearchWorkspace(string[] args)
        {
            if (args.Length == 0)
            {
                append(" Usage: /search <text> [--max N]\n", Color.Yellow);
                return;
            }
            var needle = args[0];
            int max = 20;
            for (int i = 1; i < args.Length - 1; i++)
            {
                if (args[i] == "--max" && int.TryParse(args[i + 1], out var n))
                {
                    max = Math.Clamp(n, 1, 500);
                }
            }

            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cs", ".xml", ".config", ".json", ".md", ".txt", ".resx", ".csproj" };
            int found = 0;
            foreach (var file in Directory.EnumerateFiles(WorkspaceRoot, "*", SearchOption.AllDirectories))
            {
                if (!exts.Contains(Path.GetExtension(file))) continue;

                int lineNo = 1;
                foreach (var line in File.ReadLines(file))
                {
                    if (line.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        append($" {ToWorkspaceRelative(file)}:{lineNo}: {line.Trim()}\n", Color.LightGray);
                        found++;
                        if (found >= max)
                        {
                            append(" ... more results truncated ...\n", Color.Gray);
                            return;
                        }
                    }
                    lineNo++;
                }
            }

            if (found == 0)
            {
                append(" No matches found.\n", Color.Gray);
            }
        }

        private string? ResolvePathSafe(string relative)
        {
            var combined = Path.GetFullPath(Path.Combine(WorkspaceRoot, relative));
            if (!combined.StartsWith(WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return combined;
        }

        private string ToWorkspaceRelative(string fullPath)
        {
            if (fullPath.StartsWith(WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(WorkspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        private static List<string> SplitArgs(string command)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            foreach (var ch in command)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0) { parts.Add(current.ToString()); current.Clear(); }
                }
                else
                {
                    current.Append(ch);
                }
            }
            if (current.Length > 0) parts.Add(current.ToString());
            if (parts.Count == 0) parts.Add(command);
            return parts;
        }
    }
}


