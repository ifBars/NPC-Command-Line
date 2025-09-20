using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Registry and execution engine for Codex tools following AI agent standards
    /// </summary>
    public class CodexToolRegistry
    {
        private readonly Dictionary<string, ICodexTool> tools = new();
        private readonly JsonSerializerOptions jsonOptions;

        public CodexToolRegistry()
        {
            jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            RegisterDefaultTools();
        }

        /// <summary>
        /// Get all registered tools as OpenAI/Anthropic compatible tool definitions
        /// </summary>
        public List<ToolDefinition> GetToolDefinitions()
        {
            return tools.Values.Select(tool => new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = tool.ParameterSchema
                }
            }).ToList();
        }

        /// <summary>
        /// Get tool definitions formatted for system prompt
        /// </summary>
        public string GetToolDescriptionsForPrompt()
        {
            var descriptions = tools.Values.Select(tool => 
                $"- {tool.Name}: {tool.Description}");
            return string.Join("\n", descriptions);
        }

        /// <summary>
        /// Execute a tool by name with JSON parameters
        /// </summary>
        public async Task<ToolResult> ExecuteToolAsync(string toolName, string parameters, ToolExecutionContext context)
        {
            if (!tools.TryGetValue(toolName, out var tool))
            {
                return ToolResult.CreateError($"Unknown tool: {toolName}. Available tools: {string.Join(", ", tools.Keys)}");
            }

            try
            {
                // Normalize common argument synonyms
                parameters = NormalizeArguments(toolName, parameters);
                // Validate parameters against schema if needed
                var result = await tool.ExecuteAsync(parameters, context);
                
                // Log successful execution
                if (result.Success && result.Metadata != null)
                {
                    var metadata = JsonSerializer.Serialize(result.Metadata, jsonOptions);
                    System.Diagnostics.Debug.WriteLine($"Tool {toolName} executed successfully. Metadata: {metadata}");
                }

                return result;
            }
            catch (JsonException ex)
            {
                return ToolResult.CreateError($"Invalid parameters for {toolName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"Tool {toolName} execution failed: {ex.Message}");
            }
        }

        private static string NormalizeArguments(string toolName, string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return json;

                var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value;
                }

                // Map common synonyms → canonical names
                void Map(string from, string to)
                {
                    if (dict.ContainsKey(from) && !dict.ContainsKey(to))
                    {
                        dict[to] = dict[from];
                        dict.Remove(from);
                    }
                }

                // Global mappings
                Map("file_path", "path");
                Map("filepath", "path");
                Map("dir", "path");
                Map("folder", "path");
                Map("text", "query");

                // Tool-specific tweaks (extensible)
                if (toolName.Equals("read_file", StringComparison.OrdinalIgnoreCase))
                {
                    Map("lines", "max_lines");
                    Map("start", "start_line");
                }

                // Rebuild JSON
                var buffer = new System.Buffers.ArrayBufferWriter<byte>();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    writer.WriteStartObject();
                    foreach (var kvp in dict)
                    {
                        writer.WritePropertyName(kvp.Key);
                        kvp.Value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }
                return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
            }
            catch
            {
                return json;
            }
        }

        /// <summary>
        /// Parse and execute tool calls from LLM response
        /// </summary>
        public async Task<string> ProcessToolCallsAsync(string response, ToolExecutionContext context)
        {
            try
            {
                var processedResponse = response;
                var executedCalls = new HashSet<string>();

                // 0) Replace entire code blocks that contain tool calls with plain, informative summaries
                var codeBlockPattern = new System.Text.RegularExpressions.Regex("```(?:json)?\\s*\\n?([\\s\\S]*?)```", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var codeBlockMatches = codeBlockPattern.Matches(processedResponse);
                foreach (System.Text.RegularExpressions.Match blockMatch in codeBlockMatches)
                {
                    var fullBlock = blockMatch.Value;
                    var codeContent = blockMatch.Groups[1].Value;

                    // Try to find OpenAI/Anthropic style tool calls inside the block
                    var inner = await ProcessToolCallsInsideTextAsync(codeContent, context, executedCalls);
                    if (!ReferenceEquals(inner, codeContent))
                    {
                        // If processing changed the content (i.e., executed a tool), drop the code fences
                        processedResponse = processedResponse.Replace(fullBlock, inner);
                        continue;
                    }

                    // Fallback: try to parse JSON and execute if it looks like a function tool call
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(codeContent);
                        var root = doc.RootElement;
                        if (root.ValueKind == System.Text.Json.JsonValueKind.Object
                            && root.TryGetProperty("type", out var typeProp)
                            && typeProp.GetString()?.Equals("function", StringComparison.OrdinalIgnoreCase) == true
                            && root.TryGetProperty("function", out var fnProp)
                            && fnProp.TryGetProperty("name", out var nameProp)
                            && fnProp.TryGetProperty("arguments", out var argsProp))
                        {
                            var toolName = nameProp.GetString() ?? string.Empty;
                            var argsJson = argsProp.GetRawText();

                            var callSignature = $"{toolName}({argsJson})";
                            if (executedCalls.Add(callSignature))
                            {
                                var result = await ExecuteToolAsync(toolName, argsJson, context);
                                var replacement = FormatToolResult(toolName, result);
                                processedResponse = processedResponse.Replace(fullBlock, replacement);
                            }
                        }
                    }
                    catch
                    {
                        // ignore parse errors; leave block as-is for later cleanup
                    }
                }

                // Parse OpenAI/Anthropic style tool calls
                var toolCallMatches = System.Text.RegularExpressions.Regex.Matches(
                    processedResponse, 
                    @"\{[^{}]*""type""\s*:\s*""function""[^{}]*""function""\s*:\s*\{[^{}]*""name""\s*:\s*""([^""]+)""[^{}]*""arguments""\s*:\s*(\{[\s\S]*?\})[^{}]*\}[^{}]*\}",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match match in toolCallMatches)
                {
                    var fullMatch = match.Value;
                    var toolName = match.Groups[1].Value;
                    var arguments = match.Groups[2].Value;
                    
                    var callSignature = $"{toolName}({arguments})";
                    if (!executedCalls.Add(callSignature)) continue;

                    var result = await ExecuteToolAsync(toolName, arguments, context);
                    var replacement = FormatToolResult(toolName, result);
                    
                    processedResponse = processedResponse.Replace(fullMatch, replacement);
                }

                // Parse simple function call format: function_name({"param": "value"})
                var simpleFunctionMatches = System.Text.RegularExpressions.Regex.Matches(
                    processedResponse,
                    @"(\w+)\((\{[\s\S]*?\})\)",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match match in simpleFunctionMatches)
                {
                    var fullMatch = match.Value;
                    var toolName = match.Groups[1].Value;
                    var arguments = match.Groups[2].Value;
                    
                    if (!tools.ContainsKey(toolName)) continue;
                    
                    var callSignature = $"{toolName}({arguments})";
                    if (!executedCalls.Add(callSignature)) continue;

                    var result = await ExecuteToolAsync(toolName, arguments, context);
                    var replacement = FormatToolResult(toolName, result);
                    
                    processedResponse = processedResponse.Replace(fullMatch, replacement);
                }

                // 3) Unwrap any leftover code fences that only contain tool status lines
                var cleanupMatches = codeBlockPattern.Matches(processedResponse);
                foreach (System.Text.RegularExpressions.Match block in cleanupMatches)
                {
                    var full = block.Value;
                    var inner = block.Groups[1].Value.Trim();
                    var looksLikeStatus = inner.IndexOf("🔍", StringComparison.Ordinal) >= 0
                                           || inner.IndexOf("📄", StringComparison.Ordinal) >= 0
                                           || inner.IndexOf("🔎", StringComparison.Ordinal) >= 0
                                           || inner.IndexOf("🧠", StringComparison.Ordinal) >= 0
                                           || inner.IndexOf("🌳", StringComparison.Ordinal) >= 0
                                           || inner.IndexOf("completed", StringComparison.OrdinalIgnoreCase) >= 0
                                           || inner.IndexOf("result", StringComparison.OrdinalIgnoreCase) >= 0;

                    var looksLikeCode = inner.Contains(";") || inner.Contains("class ") || inner.Contains("using ") || inner.Contains("public ") || inner.Contains("private ") || inner.Contains("{ ");

                    if (looksLikeStatus && !looksLikeCode)
                    {
                        processedResponse = processedResponse.Replace(full, inner + "\n");
                    }
                }

                return processedResponse;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing tool calls: {ex.Message}");
                return response; // Return original response if processing fails
            }
        }

        private async Task<string> ProcessToolCallsInsideTextAsync(string text, ToolExecutionContext context, HashSet<string> executedCalls)
        {
            var original = text;

            // OpenAI/Anthropic style inside arbitrary text
            var toolCallMatches = System.Text.RegularExpressions.Regex.Matches(
                text,
                @"\{[^{}]*""type""\s*:\s*""function""[^{}]*""function""\s*:\s*\{[^{}]*""name""\s*:\s*""([^""]+)""[^{}]*""arguments""\s*:\s*(\{[\s\S]*?\})[^{}]*\}[^{}]*\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in toolCallMatches)
            {
                var fullMatch = match.Value;
                var toolName = match.Groups[1].Value;
                var arguments = match.Groups[2].Value;

                var callSignature = $"{toolName}({arguments})";
                if (!executedCalls.Add(callSignature)) continue;

                var result = await ExecuteToolAsync(toolName, arguments, context);
                var replacement = FormatToolResult(toolName, result);

                text = text.Replace(fullMatch, replacement);
            }

            // Simple function style inside arbitrary text
            var simpleFunctionMatches = System.Text.RegularExpressions.Regex.Matches(
                text,
                @"(\w+)\((\{[\s\S]*?\})\)",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in simpleFunctionMatches)
            {
                var fullMatch = match.Value;
                var toolName = match.Groups[1].Value;
                var arguments = match.Groups[2].Value;

                if (!tools.ContainsKey(toolName)) continue;

                var callSignature = $"{toolName}({arguments})";
                if (!executedCalls.Add(callSignature)) continue;

                var result = await ExecuteToolAsync(toolName, arguments, context);
                var replacement = FormatToolResult(toolName, result);

                text = text.Replace(fullMatch, replacement);
            }

            return text;
        }

        /// <summary>
        /// Register a new tool
        /// </summary>
        public void RegisterTool(ICodexTool tool)
        {
            tools[tool.Name] = tool;
        }

        /// <summary>
        /// Check if a tool is registered
        /// </summary>
        public bool HasTool(string toolName)
        {
            return tools.ContainsKey(toolName);
        }

        /// <summary>
        /// Get list of registered tool names
        /// </summary>
        public IEnumerable<string> GetToolNames()
        {
            return tools.Keys;
        }

        private void RegisterDefaultTools()
        {
            RegisterTool(new ListFilesTool());
            RegisterTool(new ListTreeTool());
            RegisterTool(new ReadFileTool());
            RegisterTool(new SearchWorkspaceTool());
            RegisterTool(new SemanticSearchTool());
        }

        private static string FormatToolResult(string toolName, ToolResult result)
        {
            if (!result.Success)
            {
                return $"\n❌ {toolName} failed: {result.Error}\n";
            }

            var icon = toolName switch
            {
                "list_files" => "🔍",
                "list_tree" => "🌳", 
                "read_file" => "📄",
                "search_workspace" => "🔎",
                "semantic_search" => "🧠",
                _ => "🔧"
            };

            // Build a concise, high-signal summary for the conversation context
            var sb = new System.Text.StringBuilder();
            sb.Append('\n').Append(icon).Append(' ').Append(toolName).Append(" result");

            if (result.Metadata != null && result.Metadata.Count > 0)
            {
                // Append a compact metadata summary
                var metaPairs = result.Metadata
                    .Take(4)
                    .Select(kvp => $"{kvp.Key}={kvp.Value}");
                sb.Append(" [").Append(string.Join(", ", metaPairs)).Append(']');
            }

            sb.Append('\n');

            if (!string.IsNullOrWhiteSpace(result.Content))
            {
                // Include a trimmed slice of the content to feed back into the model's context
                var lines = (result.Content ?? string.Empty)
                    .Split('\n')
                    .Take(500) // limit to avoid flooding context
                    .ToArray();
                foreach (var line in lines)
                {
                    sb.Append(line).Append('\n');
                }
                if ((result.Content?.Split('\n').Length ?? 0) > lines.Length)
                {
                    sb.Append("...\n");
                }
            }

            return sb.ToString();
        }
    }
}
