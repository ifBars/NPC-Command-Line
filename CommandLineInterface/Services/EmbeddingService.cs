using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OllamaSharp;
using OllamaSharp.Models;

namespace CommandLineInterface.Services
{
    public class EmbeddingService
    {
        private readonly OllamaApiClient client;
        private readonly Dictionary<string, float[]> fileEmbeddings = new();
        private readonly Dictionary<string, string> fileContents = new();
        private readonly Dictionary<string, DateTime> fileModTimes = new();
        private const string EmbeddingModel = "embeddinggemma:latest";
        private const string EmbeddingCacheFile = ".embeddings_cache.json";
        
        private class GitIgnoreRule
        {
            public string Pattern { get; set; } = "";
            public bool IsNegation { get; set; }
            public bool IsDirectoryOnly { get; set; }
            public Regex? CompiledRegex { get; set; }
        }

        public EmbeddingService(Uri baseUri)
        {
            client = new OllamaApiClient(baseUri);
        }

        private List<GitIgnoreRule> LoadGitIgnoreRules(string workspaceRoot)
        {
            var rules = new List<GitIgnoreRule>();
            
            // Load .gitignore files from workspace root and all parent directories
            var currentDir = new DirectoryInfo(workspaceRoot);
            while (currentDir != null)
            {
                var gitIgnorePath = Path.Combine(currentDir.FullName, ".gitignore");
                if (File.Exists(gitIgnorePath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(gitIgnorePath);
                        foreach (var line in lines)
                        {
                            var rule = ParseGitIgnoreLine(line);
                            if (rule != null)
                            {
                                rules.Add(rule);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors reading .gitignore files
                    }
                }
                currentDir = currentDir.Parent;
            }

            // Add some default rules for common build artifacts
            rules.AddRange(new[]
            {
                ParseGitIgnoreLine("bin/"),
                ParseGitIgnoreLine("obj/"),
                ParseGitIgnoreLine(".vs/"),
                ParseGitIgnoreLine("*.user"),
                ParseGitIgnoreLine(".git/")
            }.Where(r => r != null).Cast<GitIgnoreRule>());

            return rules;
        }

        private GitIgnoreRule? ParseGitIgnoreLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                return null;

            line = line.Trim();
            if (string.IsNullOrEmpty(line))
                return null;

            var rule = new GitIgnoreRule();
            
            // Handle negation (!)
            if (line.StartsWith("!"))
            {
                rule.IsNegation = true;
                line = line.Substring(1);
            }

            // Handle directory-only patterns (trailing /)
            if (line.EndsWith("/"))
            {
                rule.IsDirectoryOnly = true;
                line = line.TrimEnd('/');
            }

            rule.Pattern = line;

            // Convert gitignore pattern to regex
            try
            {
                var regexPattern = ConvertGitIgnorePatternToRegex(line);
                rule.CompiledRegex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (Exception)
            {
                // If regex compilation fails, skip this rule
                return null;
            }

            return rule;
        }

        private string ConvertGitIgnorePatternToRegex(string pattern)
        {
            var regex = new StringBuilder();
            
            // Escape special regex characters except * and ?
            var escaped = Regex.Escape(pattern)
                .Replace("\\*", "ASTERISK_PLACEHOLDER")
                .Replace("\\?", "QUESTION_PLACEHOLDER");

            // Handle ** (matches any number of directories)
            escaped = escaped.Replace("ASTERISK_PLACEHOLDERASTERISK_PLACEHOLDER", ".*");
            
            // Handle single * (matches anything except directory separator)
            escaped = escaped.Replace("ASTERISK_PLACEHOLDER", "[^/\\\\]*");
            
            // Handle ? (matches single character except directory separator)
            escaped = escaped.Replace("QUESTION_PLACEHOLDER", "[^/\\\\]");

            // If pattern doesn't start with /, it can match anywhere in the path
            if (!pattern.StartsWith("/"))
            {
                regex.Append("(^|[/\\\\])");
            }
            else
            {
                regex.Append("^");
                escaped = escaped.Substring(1); // Remove leading /
            }

            regex.Append(escaped);

            // If pattern doesn't end with /, it matches both files and directories
            if (!pattern.EndsWith("/"))
            {
                regex.Append("($|[/\\\\])");
            }
            else
            {
                regex.Append("$");
            }

            return regex.ToString();
        }

        private bool IsIgnoredByGitIgnore(string relativePath, List<GitIgnoreRule> rules)
        {
            bool isIgnored = false;
            
            // Normalize path separators for consistent matching
            var normalizedPath = relativePath.Replace('\\', '/');
            
            foreach (var rule in rules)
            {
                if (rule.CompiledRegex == null)
                    continue;

                var matches = rule.CompiledRegex.IsMatch(normalizedPath);
                
                if (matches)
                {
                    if (rule.IsNegation)
                    {
                        isIgnored = false; // Negation rules un-ignore files
                    }
                    else
                    {
                        isIgnored = true;
                    }
                }
            }

            return isIgnored;
        }

        private class EmbeddingCache
        {
            public Dictionary<string, float[]> FileEmbeddings { get; set; } = new();
            public Dictionary<string, string> FileContents { get; set; } = new();
            public Dictionary<string, DateTime> FileModTimes { get; set; } = new();
            public string Model { get; set; } = "";
            public DateTime CacheVersion { get; set; } = DateTime.UtcNow;
        }

        public async Task<bool> IsEmbeddingModelAvailableAsync()
        {
            try
            {
                var models = await client.ListLocalModelsAsync();
                return models?.Any(m => m.Name.Contains("embeddinggemma")) ?? false;
            }
            catch
            {
                return false;
            }
        }

        public async Task IndexWorkspaceAsync(string workspaceRoot, Action<string> onProgress)
        {
            if (!await IsEmbeddingModelAvailableAsync())
            {
                onProgress("❌ embeddinggemma:latest not found. Run: ollama pull embeddinggemma:latest");
                return;
            }

            var cacheFilePath = Path.Combine(workspaceRoot, EmbeddingCacheFile);
            
            // Load existing cache if available
            await LoadCacheAsync(cacheFilePath, onProgress);

            // Load gitignore rules
            var gitIgnoreRules = LoadGitIgnoreRules(workspaceRoot);
            onProgress($"📋 Loaded {gitIgnoreRules.Count} gitignore rules");

            var codeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                ".cs", ".csproj", ".sln", ".json", ".xml", ".config", ".md", ".txt", ".resx"
            };

            var allFiles = Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories)
                .Where(f => codeExtensions.Contains(Path.GetExtension(f)))
                .Where(f => !Path.GetFileName(f).Equals(EmbeddingCacheFile, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Filter files using gitignore rules
            var files = new List<string>();
            int ignoredCount = 0;
            
            foreach (var filePath in allFiles)
            {
                var relativePath = Path.GetRelativePath(workspaceRoot, filePath);
                
                if (IsIgnoredByGitIgnore(relativePath, gitIgnoreRules))
                {
                    ignoredCount++;
                    continue;
                }
                
                files.Add(filePath);
            }
            
            if (ignoredCount > 0)
            {
                onProgress($"🚫 Filtered out {ignoredCount} files using gitignore rules");
            }

            if (files.Count == 0)
            {
                onProgress("📄 No files found to index");
                return;
            }

            // Determine which files need processing (new or modified)
            var filesToProcess = new List<string>();
            foreach (var filePath in files)
            {
                var relativePath = Path.GetRelativePath(workspaceRoot, filePath);
                var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                
                if (!fileEmbeddings.ContainsKey(relativePath) || 
                    !fileModTimes.ContainsKey(relativePath) || 
                    fileModTimes[relativePath] < lastWriteTime)
                {
                    filesToProcess.Add(filePath);
                }
            }

            if (filesToProcess.Count == 0)
            {
                onProgress($"✅ All {files.Count} files already indexed and up to date");
                return;
            }

            onProgress($"🔍 Processing {filesToProcess.Count} new/modified files (out of {files.Count} total)...");

            int processed = 0;
            foreach (var filePath in filesToProcess)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(workspaceRoot, filePath);
                    var content = await File.ReadAllTextAsync(filePath);
                    var lastWriteTime = File.GetLastWriteTimeUtc(filePath);
                    
                    // Skip very large files
                    if (content.Length > 50000) 
                    {
                        onProgress($"⚠️ Skipping large file: {Path.GetFileName(filePath)} ({content.Length:N0} chars)");
                        continue;
                    }

                    var embedding = await GenerateEmbeddingAsync(content);
                    if (embedding != null)
                    {
                        fileEmbeddings[relativePath] = embedding;
                        fileContents[relativePath] = content;
                        fileModTimes[relativePath] = lastWriteTime;
                    }

                    processed++;
                    if (processed % 5 == 0)
                    {
                        onProgress($"📄 Processed {processed}/{filesToProcess.Count} files...");
                    }
                }
                catch (Exception ex)
                {
                    onProgress($"⚠️ Failed to process {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            // Save updated cache
            await SaveCacheAsync(cacheFilePath, onProgress);
            
            var totalIndexed = fileEmbeddings.Count;
            if (processed > 0)
            {
                onProgress($"✅ Updated {processed} files. Total indexed: {totalIndexed} files");
            }
            else
            {
                onProgress($"✅ All {totalIndexed} files up to date");
            }
        }

        private async Task<float[]?> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var request = new EmbedRequest
                {
                    Model = EmbeddingModel,
                    Input = new List<string> { text.Length > 8000 ? text.Substring(0, 8000) : text }
                };

                var response = await client.EmbedAsync(request);
                return response?.Embeddings?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<(string file, float similarity, string snippet)>> SearchSemanticAsync(
            string query, int maxResults = 10)
        {
            if (fileEmbeddings.Count == 0)
            {
                return new List<(string, float, string)>();
            }

            var queryEmbedding = await GenerateEmbeddingAsync(query);
            if (queryEmbedding == null)
            {
                return new List<(string, float, string)>();
            }

            var results = new List<(string file, float similarity, string snippet)>();

            foreach (var kvp in fileEmbeddings)
            {
                var similarity = CosineSimilarity(queryEmbedding, kvp.Value);
                if (similarity > 0.3f) // Threshold for relevance
                {
                    var snippet = GetRelevantSnippet(fileContents[kvp.Key], query);
                    results.Add((kvp.Key, similarity, snippet));
                }
            }

            return results.OrderByDescending(r => r.similarity).Take(maxResults).ToList();
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;

            float dotProduct = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            return (float)(dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }

        private static string GetRelevantSnippet(string content, string query, int maxLength = 200)
        {
            var lines = content.Split('\n');
            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Find the line with most query word matches
            var bestLine = 0;
            var bestScore = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var lineScore = queryWords.Count(word => lines[i].ToLower().Contains(word));
                if (lineScore > bestScore)
                {
                    bestScore = lineScore;
                    bestLine = i;
                }
            }

            // Get context around the best line
            var start = Math.Max(0, bestLine - 2);
            var end = Math.Min(lines.Length, bestLine + 3);
            var snippet = string.Join("\n", lines[start..end]);

            return snippet.Length > maxLength ? snippet.Substring(0, maxLength) + "..." : snippet;
        }

        private async Task LoadCacheAsync(string cacheFilePath, Action<string> onProgress)
        {
            try
            {
                if (!File.Exists(cacheFilePath))
                {
                    onProgress("📄 No existing embedding cache found, will generate fresh embeddings");
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(cacheFilePath);
                var cache = JsonSerializer.Deserialize<EmbeddingCache>(jsonContent);
                
                if (cache == null)
                {
                    onProgress("⚠️ Invalid cache file format, will regenerate embeddings");
                    return;
                }

                // Check if cache was created with the same model
                if (cache.Model != EmbeddingModel)
                {
                    onProgress($"⚠️ Cache was created with different model ({cache.Model}), will regenerate embeddings");
                    return;
                }

                // Load cached data
                foreach (var kvp in cache.FileEmbeddings)
                {
                    fileEmbeddings[kvp.Key] = kvp.Value;
                }
                
                foreach (var kvp in cache.FileContents)
                {
                    fileContents[kvp.Key] = kvp.Value;
                }
                
                foreach (var kvp in cache.FileModTimes)
                {
                    fileModTimes[kvp.Key] = kvp.Value;
                }

                onProgress($"📋 Loaded {fileEmbeddings.Count} cached embeddings from {Path.GetFileName(cacheFilePath)}");
            }
            catch (Exception ex)
            {
                onProgress($"⚠️ Failed to load cache: {ex.Message}, will regenerate embeddings");
                fileEmbeddings.Clear();
                fileContents.Clear();
                fileModTimes.Clear();
            }
        }

        private async Task SaveCacheAsync(string cacheFilePath, Action<string> onProgress)
        {
            try
            {
                var cache = new EmbeddingCache
                {
                    FileEmbeddings = new Dictionary<string, float[]>(fileEmbeddings),
                    FileContents = new Dictionary<string, string>(fileContents),
                    FileModTimes = new Dictionary<string, DateTime>(fileModTimes),
                    Model = EmbeddingModel,
                    CacheVersion = DateTime.UtcNow
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = false, // Compact format to save space
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var jsonContent = JsonSerializer.Serialize(cache, options);
                await File.WriteAllTextAsync(cacheFilePath, jsonContent);
                
                var fileSize = new FileInfo(cacheFilePath).Length;
                onProgress($"💾 Saved embedding cache ({fileSize:N0} bytes) to {Path.GetFileName(cacheFilePath)}");
            }
            catch (Exception ex)
            {
                onProgress($"⚠️ Failed to save cache: {ex.Message}");
            }
        }

        public void ClearIndex()
        {
            fileEmbeddings.Clear();
            fileContents.Clear();
            fileModTimes.Clear();
        }

        public Task InvalidateCacheAsync(string workspaceRoot)
        {
            var cacheFilePath = Path.Combine(workspaceRoot, EmbeddingCacheFile);
            try
            {
                if (File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                }
                ClearIndex();
            }
            catch (Exception)
            {
                // Ignore deletion errors
            }
            
            return Task.CompletedTask;
        }

        public int IndexedFileCount => fileEmbeddings.Count;
    }
}