using System.Text;
using System.Text.Json;

namespace CommandLineInterface.Services
{
    public class EmbeddingIndexService
    {
        private readonly CodexService codexService;
        private readonly string workspaceRoot;
        private readonly string indexFilePath;

        private readonly List<EmbeddingRecord> records = new List<EmbeddingRecord>();

        private static readonly HashSet<string> DefaultExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".xml", ".config", ".json", ".md", ".txt", ".resx", ".csproj"
        };

        public EmbeddingIndexService(CodexService codexService, string workspaceRoot)
        {
            this.codexService = codexService;
            this.workspaceRoot = workspaceRoot;
            var hiddenDir = Path.Combine(workspaceRoot, ".codex");
            indexFilePath = Path.Combine(hiddenDir, "embeddings_index.json");
            TryLoadIndex();
        }

        public int Count => records.Count;

        public async Task<bool> BuildIndexAsync(Action<string, Color> append, CancellationToken cancellationToken,
            int chunkChars = 800, int strideChars = 200, IEnumerable<string>? extensions = null)
        {
            try
            {
                var allowed = extensions != null
                    ? new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase)
                    : DefaultExtensions;

                var files = Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories)
                    .Where(f => allowed.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();

                records.Clear();

                int processedFiles = 0;
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string rel = ToWorkspaceRelative(file);
                    append($" [index] {rel}\n", Color.Gray);

                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch
                    {
                        continue;
                    }

                    var lineStartOffsets = ComputeLineStartOffsets(text);
                    foreach (var (chunk, startOffset) in SlidingWindow(text, chunkChars, strideChars))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var emb = await codexService.GetEmbeddingAsync(chunk, cancellationToken: cancellationToken);
                        if (emb == null) continue;

                        int startLine = OffsetToLine(startOffset, lineStartOffsets);
                        records.Add(new EmbeddingRecord
                        {
                            FilePath = rel,
                            StartLine = startLine,
                            Text = chunk.Length > 180 ? chunk.Substring(0, 180) : chunk,
                            Vector = emb
                        });
                    }

                    processedFiles++;
                    if (processedFiles % 5 == 0)
                    {
                        append($"  files processed: {processedFiles}/{files.Count}\n", Color.Gray);
                    }
                }

                SaveIndex();
                append($" [index] built: {records.Count} chunks\n", Color.LightGreen);
                return true;
            }
            catch (OperationCanceledException)
            {
                append(" [index] cancelled\n", Color.Yellow);
                return false;
            }
            catch (Exception ex)
            {
                append($" [index] error: {ex.Message}\n", Color.Red);
                return false;
            }
        }

        public async Task<List<SearchResult>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
        {
            var results = new List<SearchResult>();
            if (records.Count == 0) return results;

            var qVec = await codexService.GetEmbeddingAsync(query, cancellationToken: cancellationToken);
            if (qVec == null) return results;

            double qNorm = VectorNorm(qVec);
            foreach (var rec in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double score = CosineSimilarity(qVec, qNorm, rec.Vector);
                results.Add(new SearchResult
                {
                    FilePath = rec.FilePath,
                    StartLine = rec.StartLine,
                    Preview = rec.Text,
                    Score = score
                });
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(Math.Clamp(topK, 1, 100))
                .ToList();
        }

        public void Clear()
        {
            records.Clear();
            try
            {
                if (File.Exists(indexFilePath)) File.Delete(indexFilePath);
            }
            catch { }
        }

        public (int chunks, long bytes) Stats()
        {
            long bytes = 0;
            try
            {
                if (File.Exists(indexFilePath))
                {
                    bytes = new FileInfo(indexFilePath).Length;
                }
            }
            catch { }
            return (records.Count, bytes);
        }

        private void SaveIndex()
        {
            try
            {
                var dir = Path.GetDirectoryName(indexFilePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var payload = new PersistedIndex
                {
                    Version = 1,
                    Model = "embeddinggemma:latest",
                    CreatedUtc = DateTime.UtcNow,
                    Items = records
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                File.WriteAllText(indexFilePath, json, Encoding.UTF8);
            }
            catch { }
        }

        private void TryLoadIndex()
        {
            try
            {
                if (!File.Exists(indexFilePath)) return;
                var json = File.ReadAllText(indexFilePath, Encoding.UTF8);
                var payload = JsonSerializer.Deserialize<PersistedIndex>(json);
                if (payload?.Items != null && payload.Items.Count > 0)
                {
                    records.Clear();
                    records.AddRange(payload.Items);
                }
            }
            catch { }
        }

        private static IEnumerable<(string chunk, int startOffset)> SlidingWindow(string text, int window, int stride)
        {
            if (string.IsNullOrEmpty(text)) yield break;
            window = Math.Max(100, window);
            stride = Math.Max(50, stride);
            for (int start = 0; start < text.Length; start += stride)
            {
                int length = Math.Min(window, text.Length - start);
                if (length <= 0) yield break;
                yield return (text.Substring(start, length), start);
                if (start + length >= text.Length) yield break;
            }
        }

        private static int[] ComputeLineStartOffsets(string text)
        {
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') starts.Add(i + 1);
            }
            return starts.ToArray();
        }

        private static int OffsetToLine(int offset, int[] starts)
        {
            int idx = Array.BinarySearch(starts, offset);
            if (idx < 0) idx = ~idx - 1;
            return Math.Max(1, idx + 1);
        }

        private static double VectorNorm(float[] v)
        {
            double sum = 0;
            for (int i = 0; i < v.Length; i++) sum += v[i] * v[i];
            return Math.Sqrt(sum);
        }

        private static double CosineSimilarity(float[] a, double aNorm, float[] b)
        {
            double dot = 0;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++) dot += a[i] * b[i];
            double bNorm = VectorNorm(b);
            if (aNorm == 0 || bNorm == 0) return 0;
            return dot / (aNorm * bNorm);
        }

        private string ToWorkspaceRelative(string fullPath)
        {
            if (fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        public class SearchResult
        {
            public required string FilePath { get; set; }
            public required int StartLine { get; set; }
            public required string Preview { get; set; }
            public required double Score { get; set; }
        }

        public class EmbeddingRecord
        {
            public required string FilePath { get; set; }
            public required int StartLine { get; set; }
            public required string Text { get; set; }
            public required float[] Vector { get; set; }
        }

        private class PersistedIndex
        {
            public int Version { get; set; }
            public string Model { get; set; } = "embeddinggemma:latest";
            public DateTime CreatedUtc { get; set; }
            public List<EmbeddingRecord> Items { get; set; } = new List<EmbeddingRecord>();
        }
    }
}

