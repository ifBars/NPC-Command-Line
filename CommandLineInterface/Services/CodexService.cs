using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OllamaSharp;
using OllamaSharp.Models;

namespace CommandLineInterface.Services
{
    public class CodexService
    {
        private readonly OllamaApiClient client;
        private readonly Uri baseUri;
        private string currentModel;

        public string CurrentModel => currentModel;
        public bool IsConnected { get; private set; }

        public CodexService(Uri baseUri, string model)
        {
            client = new OllamaApiClient(baseUri);
            this.baseUri = baseUri;
            currentModel = model;
            client.SelectedModel = model;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var models = await client.ListLocalModelsAsync();
                IsConnected = true;
                return true;
            }
            catch
            {
                IsConnected = false;
                return false;
            }
        }

        public async Task<List<Model>> GetAvailableModelsAsync()
        {
            try
            {
                var response = await client.ListLocalModelsAsync();
                return response?.ToList() ?? new List<Model>();
            }
            catch
            {
                return new List<Model>();
            }
        }

        public async Task<bool> SwitchModelAsync(string modelName)
        {
            try
            {
                // Test if model exists by attempting a simple generation
                client.SelectedModel = modelName;
                await foreach (var _ in client.GenerateAsync("test"))
                {
                    // Just test the first response
                    break;
                }
                currentModel = modelName;
                return true;
            }
            catch
            {
                // Revert to previous model if switch failed
                client.SelectedModel = currentModel;
                return false;
            }
        }

        public async Task StreamResponseAsync(string prompt, Action<string> onText, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var response in client.GenerateAsync(prompt))
                {
                    var token = response.Response ?? string.Empty;
                    if (token.Length > 0)
                        onText(token);
                }
                onText("\n");
            }
            catch (Exception ex)
            {
                onText($"\n[Error: {ex.Message}]\n");
            }
        }

        public async Task<string> PullModelAsync(string modelName, Action<string> onProgress)
        {
            try
            {
                string lastStatus = "";
                long lastPercent = -1;
                
                await foreach (var response in client.PullModelAsync(modelName))
                {
                    if (!string.IsNullOrEmpty(response.Status))
                    {
                        bool shouldUpdate = false;
                        var statusMessage = new StringBuilder();
                        
                        // Check if status changed
                        if (response.Status != lastStatus)
                        {
                            statusMessage.Append(response.Status);
                            lastStatus = response.Status;
                            shouldUpdate = true;
                        }
                        
                        // Check if progress percentage changed
                        if (response.Total > 0 && response.Completed > 0)
                        {
                            var percent = (response.Completed * 100) / response.Total;
                            if (percent != lastPercent)
                            {
                                if (!shouldUpdate)
                                {
                                    statusMessage.Append(response.Status);
                                }
                                statusMessage.Append($" ({percent}%)");
                                lastPercent = percent;
                                shouldUpdate = true;
                            }
                        }
                        
                        // Only send update if something actually changed
                        if (shouldUpdate)
                        {
                            onProgress($"{statusMessage}\n");
                        }
                    }
                }
                return "Success";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // Embeddings support via Ollama HTTP API
        private class EmbeddingsRequest
        {
            [JsonPropertyName("model")] public required string Model { get; set; }
            // Ollama embeddings API accepts "input" for the text to embed
            [JsonPropertyName("input")] public required string Input { get; set; }
        }

        private class EmbeddingsResponse
        {
            [JsonPropertyName("embedding")] public required float[] Embedding { get; set; }
        }

        public async Task<float[]?> GetEmbeddingAsync(string text, string model = "embeddinggemma:latest", CancellationToken cancellationToken = default)
        {
            try
            {
                using var http = new HttpClient { BaseAddress = baseUri };
                var req = new EmbeddingsRequest { Model = model, Input = text };
                var json = JsonSerializer.Serialize(req);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync("/api/embeddings", content, cancellationToken);
                resp.EnsureSuccessStatusCode();
                await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                var data = await JsonSerializer.DeserializeAsync<EmbeddingsResponse>(stream, cancellationToken: cancellationToken);
                return data?.Embedding;
            }
            catch
            {
                return null;
            }
        }
    }
}


