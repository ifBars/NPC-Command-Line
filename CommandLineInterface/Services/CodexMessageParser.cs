using System.Text;
using System.Text.RegularExpressions;

namespace CommandLineInterface.Services
{
    /// <summary>
    /// Enhanced message parser for Codex AI responses with rich text formatting, 
    /// markdown support, and visual indicators for different AI states
    /// </summary>
    public static class CodexMessageParser
    {
        public enum MessageType
        {
            UserMessage,
            AssistantMessage,
            SystemMessage,
            ToolCall,
            ToolResult,
            ThinkingBlock,
            CodeBlock,
            Error,
            Status
        }

        public struct ParsedMessage
        {
            public MessageType Type { get; set; }
            public string Content { get; set; }
            public Color Color { get; set; }
            public string? Icon { get; set; }
            public Dictionary<string, string>? Metadata { get; set; }
        }

        /// <summary>
        /// Parse and format a Codex response with enhanced visual formatting
        /// </summary>
        public static List<ParsedMessage> ParseMessage(string message, bool isStreaming = false)
        {
            var results = new List<ParsedMessage>();
            
            if (string.IsNullOrWhiteSpace(message))
                return results;

            // Split message into logical segments
            var segments = SplitMessageIntoSegments(message);
            
            foreach (var segment in segments)
            {
                var parsed = ParseSegment(segment, isStreaming);
                results.AddRange(parsed);
            }

            return results;
        }

        /// <summary>
        /// Apply enhanced formatting to a message for display in the terminal
        /// </summary>
        public static void DisplayMessage(List<ParsedMessage> parsedMessages, Action<string, Color> append)
        {
            foreach (var msg in parsedMessages)
            {
                DisplaySingleMessage(msg, append);
            }
        }

        private static void DisplaySingleMessage(ParsedMessage msg, Action<string, Color> append)
        {
            switch (msg.Type)
            {
                case MessageType.ToolCall:
                    DisplayToolCall(msg, append);
                    break;
                    
                case MessageType.ToolResult:
                    DisplayToolResult(msg, append);
                    break;
                    
                case MessageType.ThinkingBlock:
                    DisplayThinkingBlock(msg, append);
                    break;
                    
                case MessageType.CodeBlock:
                    DisplayCodeBlock(msg, append);
                    break;
                    
                case MessageType.Error:
                    DisplayError(msg, append);
                    break;
                    
                case MessageType.Status:
                    DisplayStatus(msg, append);
                    break;
                    
                default:
                    DisplayRegularMessage(msg, append);
                    break;
            }
        }

        private static void DisplayToolCall(ParsedMessage msg, Action<string, Color> append)
        {
            append($"\n{msg.Icon ?? "🔧"} ", Color.DeepSkyBlue);
            append("AI is using a tool", Color.Cyan);
            
            if (msg.Metadata?.ContainsKey("tool_name") == true)
            {
                append($": {msg.Metadata["tool_name"]}", Color.LightCyan);
            }
            
            append("...\n", Color.Gray);
            
            // Show parameters if available
            if (!string.IsNullOrWhiteSpace(msg.Content))
            {
                var lines = msg.Content.Split('\n');
                foreach (var line in lines.Take(3)) // Show first 3 lines of parameters
                {
                    append($"  {line}\n", Color.DarkGray);
                }
            }
        }

        private static void DisplayToolResult(ParsedMessage msg, Action<string, Color> append)
        {
            append($"{msg.Icon ?? "✅"} ", Color.LightGreen);
            append("Tool result", Color.Green);
            
            if (msg.Metadata?.ContainsKey("tool_name") == true)
            {
                append($" ({msg.Metadata["tool_name"]})", Color.DarkGreen);
            }
            
            append(":\n", Color.Gray);
            
            // Format the result content with proper indentation
            if (!string.IsNullOrWhiteSpace(msg.Content))
            {
                var lines = msg.Content.Split('\n');
                var displayLines = lines.Take(10).ToArray(); // Show first 10 lines
                
                foreach (var line in displayLines)
                {
                    if (line.Trim().StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    {
                        append($"  {line}\n", Color.Red);
                    }
                    else if (line.Trim().StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                    {
                        append($"  {line}\n", Color.Yellow);
                    }
                    else
                    {
                        append($"  {line}\n", Color.LightGray);
                    }
                }
                
                if (lines.Length > displayLines.Length)
                {
                    append($"  ... ({lines.Length - displayLines.Length} more lines)\n", Color.DarkGray);
                }
            }
        }

        private static void DisplayThinkingBlock(ParsedMessage msg, Action<string, Color> append)
        {
            append("💭 ", Color.Yellow);
            append("AI is thinking", Color.Yellow);
            append("...\n", Color.DarkGray);
            
            // Show abbreviated thinking content
            if (!string.IsNullOrWhiteSpace(msg.Content))
            {
                var summary = msg.Content.Length > 100 
                    ? msg.Content.Substring(0, 97) + "..." 
                    : msg.Content;
                append($"  \"{summary}\"\n", Color.DarkGray);
            }
        }

        private static void DisplayCodeBlock(ParsedMessage msg, Action<string, Color> append)
        {
            var language = msg.Metadata?.GetValueOrDefault("language", "text") ?? "text";
            
            append("📝 ", Color.Cyan);
            append($"Code ({language})", Color.Cyan);
            append(":\n", Color.Gray);
            
            // Basic syntax highlighting for common languages
            var lines = msg.Content.Split('\n');
            foreach (var line in lines)
            {
                var color = GetCodeLineColor(line, language);
                append($"  {line}\n", color);
            }
        }

        private static void DisplayError(ParsedMessage msg, Action<string, Color> append)
        {
            append("❌ ", Color.Red);
            append("Error: ", Color.Red);
            append($"{msg.Content}\n", Color.LightCoral);
        }

        private static void DisplayStatus(ParsedMessage msg, Action<string, Color> append)
        {
            append($"{msg.Icon ?? "ℹ️"} ", Color.DeepSkyBlue);
            append($"{msg.Content}\n", Color.LightBlue);
        }

        private static void DisplayRegularMessage(ParsedMessage msg, Action<string, Color> append)
        {
            // Enhanced markdown-like formatting for regular AI responses
            var lines = msg.Content.Split('\n');
            
            foreach (var line in lines)
            {
                var formattedLine = FormatMarkdownLine(line);
                append($"{formattedLine}\n", msg.Color);
            }
        }

        private static string FormatMarkdownLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return line;

            // Headers
            if (line.StartsWith("### "))
                return $"  {line.Substring(4)}"; // Remove ### and add indent
            if (line.StartsWith("## "))
                return $" {line.Substring(3)}"; // Remove ## and add indent
            if (line.StartsWith("# "))
                return line.Substring(2); // Remove #

            // Lists
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                return $"  • {line.TrimStart().Substring(2)}";
            
            // Numbered lists
            var numberedMatch = Regex.Match(line.TrimStart(), @"^(\d+)\.\s+(.*)");
            if (numberedMatch.Success)
                return $"  {numberedMatch.Groups[1].Value}. {numberedMatch.Groups[2].Value}";

            return line;
        }

        private static Color GetCodeLineColor(string line, string language)
        {
            if (string.IsNullOrWhiteSpace(line))
                return Color.LightGray;

            var trimmed = line.Trim();

            // Comments
            if (language.ToLower() is "csharp" or "c#" or "cs")
            {
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*"))
                    return Color.Green;
                if (trimmed.Contains("using ") && trimmed.EndsWith(";"))
                    return Color.Blue;
                if (Regex.IsMatch(trimmed, @"\b(public|private|protected|internal|static|class|interface|enum)\b"))
                    return Color.Cyan;
            }
            else if (language.ToLower() is "json")
            {
                if (trimmed.Contains("\"") && trimmed.Contains(":"))
                    return Color.Yellow;
            }

            return Color.LightGray;
        }

        private static List<string> SplitMessageIntoSegments(string message)
        {
            var segments = new List<string>();
            
            // Split on major boundaries while preserving content
            var patterns = new[]
            {
                @"<think>[\s\S]*?</think>",           // Thinking blocks
                @"```[\s\S]*?```",                   // Code blocks
                @"\{[^{}]*""type""\s*:\s*""function""[^{}]*\}",  // Tool calls
                @"🔍[^\n]*\n(?:[^\n🔍🌳📄🔎🧠❌]*\n)*", // Tool results starting with emoji
                @"🌳[^\n]*\n(?:[^\n🔍🌳📄🔎🧠❌]*\n)*",
                @"📄[^\n]*\n(?:[^\n🔍🌳📄🔎🧠❌]*\n)*",
                @"🔎[^\n]*\n(?:[^\n🔍🌳📄🔎🧠❌]*\n)*",
                @"🧠[^\n]*\n(?:[^\n🔍🌳📄🔎🧠❌]*\n)*",
                @"❌[^\n]*(?:\n[^\n🔍🌳📄🔎🧠❌]*)*"
            };

            var allMatches = new List<(int start, int length, string content)>();
            
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(message, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    allMatches.Add((match.Index, match.Length, match.Value));
                }
            }

            // Sort by position and extract segments
            allMatches.Sort((a, b) => a.start.CompareTo(b.start));
            
            int lastEnd = 0;
            foreach (var (start, length, content) in allMatches)
            {
                // Add text before this match
                if (start > lastEnd)
                {
                    var beforeContent = message.Substring(lastEnd, start - lastEnd).Trim();
                    if (!string.IsNullOrWhiteSpace(beforeContent))
                        segments.Add(beforeContent);
                }
                
                // Add the matched content
                segments.Add(content);
                lastEnd = start + length;
            }

            // Add remaining content
            if (lastEnd < message.Length)
            {
                var remaining = message.Substring(lastEnd).Trim();
                if (!string.IsNullOrWhiteSpace(remaining))
                    segments.Add(remaining);
            }

            return segments.Count > 0 ? segments : new List<string> { message };
        }

        private static List<ParsedMessage> ParseSegment(string segment, bool isStreaming)
        {
            var results = new List<ParsedMessage>();
            
            if (string.IsNullOrWhiteSpace(segment))
                return results;

            // Thinking blocks
            var thinkMatch = Regex.Match(segment, @"<think>([\s\S]*?)</think>", RegexOptions.IgnoreCase);
            if (thinkMatch.Success)
            {
                results.Add(new ParsedMessage
                {
                    Type = MessageType.ThinkingBlock,
                    Content = thinkMatch.Groups[1].Value.Trim(),
                    Color = Color.Yellow,
                    Icon = "💭"
                });
                return results;
            }

            // Code blocks
            var codeMatch = Regex.Match(segment, @"```(\w+)?\n?([\s\S]*?)```", RegexOptions.IgnoreCase);
            if (codeMatch.Success)
            {
                var language = codeMatch.Groups[1].Success ? codeMatch.Groups[1].Value : "text";
                results.Add(new ParsedMessage
                {
                    Type = MessageType.CodeBlock,
                    Content = codeMatch.Groups[2].Value,
                    Color = Color.LightGray,
                    Icon = "📝",
                    Metadata = new Dictionary<string, string> { ["language"] = language }
                });
                return results;
            }

            // Tool calls (JSON format)
            var toolCallMatch = Regex.Match(segment, @"\{[^{}]*""type""\s*:\s*""function""[^{}]*\}", RegexOptions.IgnoreCase);
            if (toolCallMatch.Success)
            {
                var toolName = ExtractToolName(toolCallMatch.Value);
                results.Add(new ParsedMessage
                {
                    Type = MessageType.ToolCall,
                    Content = toolCallMatch.Value,
                    Color = Color.Cyan,
                    Icon = GetToolIcon(toolName),
                    Metadata = new Dictionary<string, string> { ["tool_name"] = toolName }
                });
                return results;
            }

            // Tool results (emoji-prefixed)
            var toolResultMatch = Regex.Match(segment, @"^([🔍🌳📄🔎🧠])\s*([^\n]*)\n?([\s\S]*)", RegexOptions.Multiline);
            if (toolResultMatch.Success)
            {
                var icon = toolResultMatch.Groups[1].Value;
                var title = toolResultMatch.Groups[2].Value;
                var content = toolResultMatch.Groups[3].Value;
                
                results.Add(new ParsedMessage
                {
                    Type = MessageType.ToolResult,
                    Content = content,
                    Color = Color.LightGreen,
                    Icon = icon,
                    Metadata = new Dictionary<string, string> 
                    { 
                        ["tool_name"] = GetToolNameFromIcon(icon),
                        ["title"] = title
                    }
                });
                return results;
            }

            // Error messages
            if (segment.StartsWith("❌") || segment.ToLower().Contains("error:"))
            {
                results.Add(new ParsedMessage
                {
                    Type = MessageType.Error,
                    Content = segment.Replace("❌", "").Trim(),
                    Color = Color.Red,
                    Icon = "❌"
                });
                return results;
            }

            // Regular message
            results.Add(new ParsedMessage
            {
                Type = MessageType.AssistantMessage,
                Content = segment,
                Color = Color.LightGray
            });

            return results;
        }

        private static string ExtractToolName(string toolCallJson)
        {
            var nameMatch = Regex.Match(toolCallJson, @"""name""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            return nameMatch.Success ? nameMatch.Groups[1].Value : "unknown_tool";
        }

        private static string GetToolIcon(string toolName)
        {
            return toolName.ToLower() switch
            {
                "list_files" => "🔍",
                "list_tree" => "🌳",
                "read_file" => "📄",
                "search_workspace" => "🔎",
                "semantic_search" => "🧠",
                _ => "🔧"
            };
        }

        private static string GetToolNameFromIcon(string icon)
        {
            return icon switch
            {
                "🔍" => "list_files",
                "🌳" => "list_tree",
                "📄" => "read_file",
                "🔎" => "search_workspace",
                "🧠" => "semantic_search",
                _ => "unknown_tool"
            };
        }

        /// <summary>
        /// Create a streaming progress indicator for AI thinking
        /// </summary>
        public static void ShowThinkingIndicator(Action<string, Color> append, string phase = "thinking")
        {
            var indicators = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            var phaseEmoji = phase.ToLower() switch
            {
                "thinking" => "💭",
                "processing" => "⚙️",
                "analyzing" => "🔍",
                "generating" => "✨",
                _ => "🤖"
            };
            
            append($"{phaseEmoji} AI is {phase}", Color.Yellow);
            // Note: In a real implementation, you'd cycle through indicators with a timer
            append("...\n", Color.DarkGray);
        }

        /// <summary>
        /// Format a progress message with visual indicators
        /// </summary>
        public static void ShowProgress(Action<string, Color> append, string message, int percentage = -1)
        {
            append("⏳ ", Color.Cyan);
            if (percentage >= 0)
            {
                var bar = CreateProgressBar(percentage);
                append($"{message} {bar} {percentage}%\n", Color.LightCyan);
            }
            else
            {
                append($"{message}\n", Color.LightCyan);
            }
        }

        private static string CreateProgressBar(int percentage, int width = 20)
        {
            var filled = (int)Math.Round(width * percentage / 100.0);
            var empty = width - filled;
            return $"[{new string('█', filled)}{new string('░', empty)}]";
        }
    }
}
