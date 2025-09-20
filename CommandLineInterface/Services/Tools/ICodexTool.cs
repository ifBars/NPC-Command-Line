using System.Text.Json.Serialization;

namespace CommandLineInterface.Services.Tools
{
    /// <summary>
    /// Represents a tool that can be called by the AI agent
    /// </summary>
    public interface ICodexTool
    {
        /// <summary>
        /// Unique identifier for the tool
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Human-readable description of what the tool does
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// JSON schema for the tool's parameters
        /// </summary>
        object ParameterSchema { get; }
        
        /// <summary>
        /// Execute the tool with the given parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as JSON string</param>
        /// <param name="context">Execution context</param>
        /// <returns>Tool execution result</returns>
        Task<ToolResult> ExecuteAsync(string parameters, ToolExecutionContext context);
    }

    /// <summary>
    /// Result of a tool execution
    /// </summary>
    public class ToolResult
    {
        public bool Success { get; set; }
        public string? Content { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

        public static ToolResult CreateSuccess(string content, Dictionary<string, object>? metadata = null)
        {
            return new ToolResult { Success = true, Content = content, Metadata = metadata };
        }

        public static ToolResult CreateError(string error)
        {
            return new ToolResult { Success = false, Error = error };
        }
    }

    /// <summary>
    /// Context for tool execution
    /// </summary>
    public class ToolExecutionContext
    {
        public string WorkspaceRoot { get; set; } = string.Empty;
        public Action<string, Color> OutputAppender { get; set; } = (_, _) => { };
        public EmbeddingService? EmbeddingService { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>
    /// Standard tool definition for OpenAI/Anthropic function calling
    /// </summary>
    public class ToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public FunctionDefinition Function { get; set; } = new();
    }

    public class FunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public object Parameters { get; set; } = new();
    }
}
