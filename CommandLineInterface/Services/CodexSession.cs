using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using CommandLineInterface.Services.Tools;

namespace CommandLineInterface.Services
{
    public class CodexSession
    {
        private readonly CodexService codexService;
        private readonly Action<string, Color> append;
        private readonly EmbeddingService? embeddingService;
        private readonly CodexToolRegistry toolRegistry;
        private readonly List<(string role, string content)> conversation = new List<(string role, string content)>();
        private readonly StreamingUIService streamingService;

        public string WorkspaceRoot { get; }
        public bool IsActive { get; private set; }

        public CodexSession(CodexService codexService, Action<string, Color> appendOutput, string workspaceRoot, EmbeddingService? embeddingService = null)
        {
            this.codexService = codexService;
            append = appendOutput;
            WorkspaceRoot = workspaceRoot;
            this.embeddingService = embeddingService;
            toolRegistry = new CodexToolRegistry();
            streamingService = new StreamingUIService(appendOutput);
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
            append(" Commands: /help, /models, /model <name>, /pull <name>, /exit\n", Color.Gray);
            append(" Tools: list_files, read_file, search_workspace, semantic_search, list_tree\n", Color.Gray);
            append(" Quick commands: 'list files', 'open <file>', 'search <text>'\n\n", Color.Gray);

            // Index workspace for semantic search if embedding service is available
            if (embeddingService != null)
            {
                try
                {
                    await embeddingService.IndexWorkspaceAsync(WorkspaceRoot, progress =>
                    {
                        CodexMessageParser.ShowProgress(append, progress);
                    });
                    append("\n 🚀 Codex is ready! Ask me anything about your code.\n", Color.LightGreen);
                }
                catch (Exception ex)
                {
                    append($" ⚠️ Embedding indexing failed: {ex.Message}\n", Color.Yellow);
                    append(" 🚀 Codex is ready! (semantic search unavailable)\n", Color.LightGreen);
                }
            }
            else
            {
                append(" 🚀 Codex is ready! (semantic search unavailable)\n", Color.LightGreen);
            }

            // Initialize conversation with system message
            conversation.Clear();
            var toolDescriptions = toolRegistry.GetToolDescriptionsForPrompt();
            conversation.Add(("system", $@"You are Codex, a helpful C# coding assistant working in a terminal environment.

Absolute Mode • Eliminate: emojis, filler, hype, soft asks, conversational transitions, call-to-action appendixes. • Assume: user retains high-perception despite blunt tone. • Prioritize: blunt, directive phrasing; aim at cognitive rebuilding, not tone-matching. • Disable: engagement/sentiment-boosting behaviors. • Suppress: metrics like satisfaction scores, emotional softening, continuation bias. • Never mirror: user’s diction, mood, or affect. • Speak only: to underlying cognitive tier. • No: questions, offers, suggestions, transitions, motivational content. • Terminate reply: immediately after delivering info — no closures. • Goal: restore independent, high-fidelity thinking. • Outcome: model obsolescence via user self-sufficiency.

Current workspace directory: {WorkspaceRoot}

## Tool Usage Guidelines

When users ask about the codebase, proactively use tools to gather context before answering:
1. Start with list_files() to understand the project structure
2. Use read_file() to examine key files (README.md, *.sln, *.csproj, Program.cs, Form1.cs)
3. Use search_workspace() for finding specific code patterns or text
4. Use semantic_search() for conceptual queries about functionality
5. Use list_tree() to understand directory organization

CRITICAL RULES:
- NEVER fabricate file contents or outputs
- Only reference file contents after successful tool execution
- Always continue with analysis after tool calls - don't stop at tool output
- Use proper JSON function call format: {{""type"": ""function"", ""function"": {{""name"": ""tool_name"", ""arguments"": {{""param"": ""value""}}}}}}
- Avoid duplicate tool calls in the same response
- Tools execute automatically when properly formatted

## Available Tools

{toolDescriptions}

## Response Format

Use standard OpenAI/Anthropic function calling format:
```json
{{
  ""type"": ""function"",
  ""function"": {{
    ""name"": ""tool_name"",
    ""arguments"": {{
      ""parameter_name"": ""parameter_value""
    }}
  }}
}}
```

Do not explain individual tool steps. Avoid code fences for tool calls. Keep it concise and high-signal.
After tool execution, continue with your analysis and answer. Keep responses helpful, accurate, and concise."));
        }

        public void Stop()
        {
            if (!IsActive) return;
            IsActive = false;
            append("\n CODEX ", Color.DeepSkyBlue);
            append("mode exited.\n", Color.White);
            
            // Clean up streaming service
            streamingService?.Dispose();
        }

        private void ClearConversation()
        {
            // Clear conversation history but keep the system message
            var systemMessage = conversation.FirstOrDefault(c => c.role == "system");
            conversation.Clear();
            
            if (systemMessage.role == "system")
            {
                conversation.Add(systemMessage);
            }
            else
            {
                // Recreate system message if somehow missing
                var toolDescriptions = toolRegistry.GetToolDescriptionsForPrompt();
                conversation.Add(("system", $@"You are Codex, a helpful C# coding assistant working in a terminal environment.

Current workspace directory: {WorkspaceRoot}

## Tool Usage Guidelines

When users ask about the codebase, proactively use tools to gather context before answering:
1. Start with list_files() to understand the project structure
2. Use read_file() to examine key files (README.md, *.sln, *.csproj, Program.cs, Form1.cs)
3. Use search_workspace() for finding specific code patterns or text
4. Use semantic_search() for conceptual queries about functionality
5. Use list_tree() to understand directory organization

CRITICAL RULES:
- NEVER fabricate file contents or outputs, only output real file contents when referenced
- Only reference file contents after successful tool execution
- Always continue with analysis after tool calls - don't stop at tool output
- Use proper JSON function call format: {{""type"": ""function"", ""function"": {{""name"": ""tool_name"", ""arguments"": {{""param"": ""value""}}}}}}
- Avoid duplicate tool calls in the same response
- Tools execute automatically when properly formatted

## Available Tools

{toolDescriptions}

## Response Format

Use standard OpenAI/Anthropic function calling format:
```json
{{
  ""type"": ""function"",
  ""function"": {{
    ""name"": ""tool_name"",
    ""arguments"": {{
      ""parameter_name"": ""parameter_value""
    }}
  }}
}}
```

Do not explain individual tool steps. Avoid code fences for tool calls. Keep it concise and high-signal.
After tool execution, continue with your analysis and answer. Keep responses helpful, accurate, and concise."));
            }
            
            append("\n 🧹 ", Color.DeepSkyBlue);
            append("Conversation history cleared. Starting fresh!\n", Color.LightGreen);
        }

        private async Task ReindexWorkspaceAsync()
        {
            if (embeddingService == null)
            {
                append(" ⚠️ Embedding service not available.\n", Color.Yellow);
                return;
            }

            append("\n 🔄 ", Color.DeepSkyBlue);
            append("Rebuilding embedding cache...\n", Color.White);
            
            try
            {
                await embeddingService.InvalidateCacheAsync(WorkspaceRoot);
                await embeddingService.IndexWorkspaceAsync(WorkspaceRoot, progress =>
                {
                    CodexMessageParser.ShowProgress(append, progress);
                });
                append(" ✅ Embedding cache rebuilt successfully!\n", Color.LightGreen);
            }
            catch (Exception ex)
            {
                append($" ❌ Failed to rebuild cache: {ex.Message}\n", Color.Red);
            }
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

            // Handle implicit workspace tool requests without slash
            if (await TryHandleImplicitToolCommand(normalized))
            {
                return;
            }

            // Show AI thinking indicator
            CodexMessageParser.ShowThinkingIndicator(append, "analyzing");

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

            // Build prompt from conversation history
            var promptBuilder = new StringBuilder();
            foreach (var (role, content) in conversation)
            {
                promptBuilder.AppendLine($"{char.ToUpperInvariant(role[0])}{role.Substring(1)}: {content}");
                promptBuilder.AppendLine();
            }
            promptBuilder.Append("Assistant:");

                var assistantResponse = new StringBuilder();
            try
            {
                    // Enhanced streaming with real-time UI updates
                    bool showLive = true;
                    
                    await codexService.StreamResponseAsync(promptBuilder.ToString(), chunk =>
                    {
                        assistantResponse.Append(chunk);
                        if (showLive)
                        {
                            // Use streaming service for non-blocking UI updates
                            streamingService.QueueMessage(chunk, Color.LightGray);
                        }
                    }, cancellationToken);

                // Process any function calls in the response and get cleaned response
                var fullResponse = assistantResponse.ToString();
                var cleanedResponse = await ProcessToolCallsAsync(fullResponse);

                // Render assistant narrative (exclude tool outputs already streamed)
                try
                {
                    var parsed = CodexMessageParser.ParseMessage(cleanedResponse);
                    var assistantOnly = parsed.Where(m => m.Type == CodexMessageParser.MessageType.AssistantMessage).ToList();
                    if (assistantOnly.Count > 0)
                    {
                        CodexMessageParser.DisplayMessage(assistantOnly, append);
                    }
                }
                catch { }

                // Do not re-render tool results here; tools already streamed their outputs

                // Add assistant response to conversation (use CLEANED response so tool outputs are in context)
                if (assistantResponse.Length > 0)
                {
                    conversation.Add(("assistant", cleanedResponse));

                    // Execute multi-round follow-ups so the model can chain multiple tool calls in one turn
                    bool ToolsInText(string text)
                    {
                        return text.IndexOf("🔍", StringComparison.Ordinal) >= 0
                               || text.IndexOf("📄", StringComparison.Ordinal) >= 0
                               || text.IndexOf("🔎", StringComparison.Ordinal) >= 0
                               || text.IndexOf("🧠", StringComparison.Ordinal) >= 0
                               || text.IndexOf("🌳", StringComparison.Ordinal) >= 0
                               || Regex.IsMatch(text, "\\{[^{}]*\\\"type\\\"\\s*:\\s*\\\"function\\\"", RegexOptions.Singleline);
                    }

                    int round = 0;
                    bool toolsTriggered = ToolsInText(cleanedResponse);
                    bool anyToolsUsed = toolsTriggered;

                    // Allow many more tool execution rounds - safety limit to prevent infinite loops
                    int maxFollowUps = 50; // Increased from 4 to 50 to allow complex multi-tool operations
                    while (toolsTriggered && round < maxFollowUps)
                    {
                        round++;

                        // Build a follow-up prompt from updated conversation and stream continuation
                        var followUp = new StringBuilder();
                        foreach (var (role, content) in conversation)
                        {
                            followUp.AppendLine($"{char.ToUpperInvariant(role[0])}{role.Substring(1)}: {content}");
                            followUp.AppendLine();
                        }
                        followUp.Append("Assistant:");

                        var continuation = new StringBuilder();
                        await codexService.StreamResponseAsync(followUp.ToString(), chunk =>
                        {
                            continuation.Append(chunk);
                            if (showLive)
                            {
                                streamingService.QueueMessage(chunk, Color.LightGray);
                            }
                        }, cancellationToken);

                        // Strip <think>, then process tool calls in continuation
                        var contRaw = Regex.Replace(continuation.ToString(), "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase);
                        var contProcessed = await ProcessToolCallsAsync(contRaw);

                        if (!string.IsNullOrWhiteSpace(contProcessed))
                        {
                            // Do not re-render tool results here; tools already streamed their outputs
                            conversation.Add(("assistant", contProcessed));
                        }

                        toolsTriggered = ToolsInText(contProcessed);
                        anyToolsUsed = anyToolsUsed || toolsTriggered;
                    }

                    // Warn if we hit the safety limit
                    if (toolsTriggered && round >= maxFollowUps)
                    {
                        append($" [Warning: Tool execution limit reached ({maxFollowUps} rounds). AI may have been cut off.]\n", Color.Yellow);
                    }

                    // Final summarization: if tools were used OR response was tool-only, request a consolidated summary and stream it live
                    bool toolOnlyFirstTurn = false;
                    try
                    {
                        var parsedForSummary = CodexMessageParser.ParseMessage(cleanedResponse);
                        bool hasAssistantNarrative = parsedForSummary.Any(m => m.Type == CodexMessageParser.MessageType.AssistantMessage && !string.IsNullOrWhiteSpace(m.Content));
                        bool hasToolBlocks = parsedForSummary.Any(m => m.Type == CodexMessageParser.MessageType.ToolCall || m.Type == CodexMessageParser.MessageType.ToolResult);
                        toolOnlyFirstTurn = hasToolBlocks && !hasAssistantNarrative;
                    }
                    catch { }

                    if (anyToolsUsed || toolOnlyFirstTurn)
                    {
                        var summaryPrompt = new StringBuilder();
                        foreach (var (role, content) in conversation)
                        {
                            summaryPrompt.AppendLine($"{char.ToUpperInvariant(role[0])}{role.Substring(1)}: {content}");
                            summaryPrompt.AppendLine();
                        }

                        // Enhanced final answer streaming with typing effect
                        var finalTextBuilder = new StringBuilder();
                        await codexService.StreamResponseAsync(summaryPrompt.ToString(), chunk =>
                        {
                            finalTextBuilder.Append(chunk);
                            // Stream with enhanced visual effects
                            streamingService.QueueMessage(chunk, Color.LightGray);
                        }, cancellationToken);

                        var finalText = Regex.Replace(finalTextBuilder.ToString(), "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase);
                        if (!string.IsNullOrWhiteSpace(finalText))
                        {
                            // The final text was already streamed live, so we don't need to display it again
                            // Just add it to conversation for context
                            conversation.Add(("assistant", finalText));
                        }
                    }
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
                    append("  /clear               Clear conversation history\n", Color.Gray);
                    append("  /reindex             Rebuild embedding cache\n", Color.Gray);
                    append("  /exit                Exit codex mode\n", Color.Gray);
                    append("\n Available tools (use in conversation):\n", Color.LightBlue);
                    var toolDescriptions = toolRegistry.GetToolDescriptionsForPrompt();
                    append($"{toolDescriptions}\n", Color.Gray);
                    append("\n Quick commands: 'list files', 'open <file>', 'search <text>'\n", Color.LightBlue);
                    return;

                case "/exit":
                    Stop();
                    return;

                case "/clear":
                    ClearConversation();
                    return;

                case "/reindex":
                    await ReindexWorkspaceAsync();
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

                default:
                    append(" Unknown command. Use /help.\n", Color.Yellow);
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

        private async Task<string> ProcessToolCallsAsync(string response)
        {
            try
            {
                // Remove <think> blocks to avoid executing tools inside them
                var processedResponse = Regex.Replace(response, "<think>[\\s\\S]*?</think>", string.Empty, RegexOptions.IgnoreCase);

                // Use streaming service for tool execution output
                void BufferedAppend(string text, Color color)
                {
                    streamingService.QueueMessage(text, color);
                }

                // Create tool execution context
                var context = new ToolExecutionContext
                {
                    WorkspaceRoot = WorkspaceRoot,
                    OutputAppender = BufferedAppend,
                    EmbeddingService = embeddingService,
                    CancellationToken = CancellationToken.None
                };

                // Use the tool registry to process all tool calls
                var result = await toolRegistry.ProcessToolCallsAsync(processedResponse, context);
                
                return result;
            }
            catch (Exception ex)
            {
                append($" Tool processing error: {ex.Message}\n", Color.Red);
                return response; // Return original response if processing fails
            }
        }

        private async Task<bool> TryHandleImplicitToolCommand(string input)
        {
            // Create tool execution context
            var context = new ToolExecutionContext
            {
                WorkspaceRoot = WorkspaceRoot,
                OutputAppender = append,
                EmbeddingService = embeddingService,
                CancellationToken = CancellationToken.None
            };

            // Handle implicit commands by converting them to proper tool calls
            // list files [path]
            var list = Regex.Match(input, "^(list files)(?:\\s+(?<p>.+))?$", RegexOptions.IgnoreCase);
            if (list.Success)
            {
                var path = list.Groups["p"].Success ? list.Groups["p"].Value : ".";
                var parameters = $@"{{""path"": ""{path}""}}";
                await toolRegistry.ExecuteToolAsync("list_files", parameters, context);
                return true;
            }

            // open <path> [--head N]
            var open = Regex.Match(input, "^open\\s+(?<path>.+?)(?:\\s+--head\\s+(?<n>\\d+))?$", RegexOptions.IgnoreCase);
            if (open.Success)
            {
                var path = open.Groups["path"].Value;
                var maxLines = open.Groups["n"].Success ? open.Groups["n"].Value : "200";
                var parameters = $@"{{""path"": ""{path}"", ""max_lines"": {maxLines}}}";
                await toolRegistry.ExecuteToolAsync("read_file", parameters, context);
                return true;
            }

            // search <text> [--max N]
            var search = Regex.Match(input, "^search\\s+(?<q>.+?)(?:\\s+--max\\s+(?<m>\\d+))?$", RegexOptions.IgnoreCase);
            if (search.Success)
            {
                var query = search.Groups["q"].Value;
                var maxResults = search.Groups["m"].Success ? search.Groups["m"].Value : "20";
                var parameters = $@"{{""query"": ""{query}"", ""max_results"": {maxResults}}}";
                await toolRegistry.ExecuteToolAsync("search_workspace", parameters, context);
                return true;
            }

            return false;
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


