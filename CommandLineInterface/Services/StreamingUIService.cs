using System.Collections.Concurrent;
using System.Text;

namespace CommandLineInterface.Services
{
    /// <summary>
    /// Enhanced streaming service for real-time UI updates without blocking the main thread
    /// </summary>
    public class StreamingUIService : IDisposable
    {
        private readonly Action<string, Color> appendCallback;
        private readonly ConcurrentQueue<(string text, Color color)> messageQueue;
        private readonly System.Threading.Timer uiUpdateTimer;
        private readonly object lockObject = new object();
        private bool isDisposed = false;

        public StreamingUIService(Action<string, Color> appendCallback)
        {
            this.appendCallback = appendCallback;
            messageQueue = new ConcurrentQueue<(string, Color)>();
            
            // Create a high-frequency timer for smooth UI updates
            uiUpdateTimer = new System.Threading.Timer(FlushUIUpdates, null, TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16)); // ~60 FPS
        }

        /// <summary>
        /// Queue a message for UI display without blocking
        /// </summary>
        public void QueueMessage(string text, Color color)
        {
            if (isDisposed) return;
            
            messageQueue.Enqueue((text, color));
        }

        /// <summary>
        /// Stream text character by character for real-time effect
        /// </summary>
        public void StreamText(string text, Color color, int delayMs = 1)
        {
            if (isDisposed) return;

            Task.Run(async () =>
            {
                foreach (char c in text)
                {
                    if (isDisposed) break;
                    
                    QueueMessage(c.ToString(), color);
                    
                    if (delayMs > 0)
                        await Task.Delay(delayMs);
                }
            });
        }

        /// <summary>
        /// Stream with typing effect for enhanced user experience
        /// </summary>
        public async Task StreamWithTypingEffect(string text, Color color, int baseDelayMs = 30)
        {
            if (isDisposed) return;

            var random = new Random();
            var words = text.Split(' ');
            
            await Task.Run(async () =>
            {
                for (int i = 0; i < words.Length; i++)
                {
                    if (isDisposed) break;
                    
                    var word = words[i];
                    
                    // Add some randomness to typing speed
                    var wordDelay = baseDelayMs + random.Next(-10, 20);
                    
                    foreach (char c in word)
                    {
                        if (isDisposed) break;
                        QueueMessage(c.ToString(), color);
                        await Task.Delay(Math.Max(1, wordDelay / 3));
                    }
                    
                    // Add space between words (except for last word)
                    if (i < words.Length - 1)
                    {
                        QueueMessage(" ", color);
                        await Task.Delay(wordDelay);
                    }
                }
            });
        }

        /// <summary>
        /// Create a real-time progress indicator
        /// </summary>
        public IDisposable CreateProgressIndicator(string message, Color color)
        {
            return new ProgressIndicator(this, message, color);
        }

        private void FlushUIUpdates(object? state)
        {
            if (isDisposed) return;

            var batch = new StringBuilder();
            Color? currentColor = null;
            int processedCount = 0;
            const int maxBatchSize = 100; // Prevent overwhelming the UI

            // Process messages in batches for better performance
            while (messageQueue.TryDequeue(out var message) && processedCount < maxBatchSize)
            {
                if (currentColor == null)
                {
                    currentColor = message.color;
                    batch.Append(message.text);
                }
                else if (currentColor == message.color)
                {
                    batch.Append(message.text);
                }
                else
                {
                    // Color changed, flush current batch and start new one
                    if (batch.Length > 0)
                    {
                        try
                        {
                            appendCallback(batch.ToString(), currentColor.Value);
                        }
                        catch
                        {
                            // Ignore UI errors if form is disposed
                        }
                        batch.Clear();
                    }
                    currentColor = message.color;
                    batch.Append(message.text);
                }
                processedCount++;
            }

            // Flush remaining batch
            if (batch.Length > 0 && currentColor.HasValue)
            {
                try
                {
                    appendCallback(batch.ToString(), currentColor.Value);
                }
                catch
                {
                    // Ignore UI errors if form is disposed
                }
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            isDisposed = true;
            uiUpdateTimer?.Dispose();
            
            // Flush any remaining messages
            FlushUIUpdates(null);
        }

        private class ProgressIndicator : IDisposable
        {
            private readonly StreamingUIService parent;
            private readonly System.Threading.Timer timer;
            private readonly string[] spinnerChars = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            private readonly string message;
            private readonly Color color;
            private int spinnerIndex = 0;
            private bool disposed = false;

            public ProgressIndicator(StreamingUIService parent, string message, Color color)
            {
                this.parent = parent;
                this.message = message;
                this.color = color;
                
                timer = new System.Threading.Timer(UpdateSpinner, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
            }

            private void UpdateSpinner(object? state)
            {
                if (disposed) return;

                var spinner = spinnerChars[spinnerIndex % spinnerChars.Length];
                parent.QueueMessage($"\r{spinner} {message}", color);
                spinnerIndex++;
            }

            public void Dispose()
            {
                if (disposed) return;
                
                disposed = true;
                timer?.Dispose();
                parent.QueueMessage($"\r✓ {message}\n", color);
            }
        }
    }

    /// <summary>
    /// Extension methods for enhanced streaming capabilities
    /// </summary>
    public static class StreamingExtensions
    {
        /// <summary>
        /// Create a streaming wrapper for the append callback
        /// </summary>
        public static StreamingUIService CreateStreamingService(this Action<string, Color> appendCallback)
        {
            return new StreamingUIService(appendCallback);
        }

        /// <summary>
        /// Stream AI response with enhanced visual effects
        /// </summary>
        public static async Task StreamAIResponse(this StreamingUIService streaming, string response, Color color)
        {
            // Parse the response for different types of content
            var lines = response.Split('\n');
            
            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("```"))
                {
                    // Code block - stream faster
                    streaming.QueueMessage(line + "\n", Color.Cyan);
                }
                else if (line.Trim().StartsWith("#"))
                {
                    // Header - stream with emphasis
                    await streaming.StreamWithTypingEffect(line, Color.Yellow, 20);
                    streaming.QueueMessage("\n", color);
                }
                else if (line.Trim().StartsWith("- ") || line.Trim().StartsWith("* "))
                {
                    // List item - stream normally
                    await streaming.StreamWithTypingEffect(line, color, 25);
                    streaming.QueueMessage("\n", color);
                }
                else if (!string.IsNullOrWhiteSpace(line))
                {
                    // Regular text - stream with natural typing effect
                    await streaming.StreamWithTypingEffect(line, color, 30);
                    streaming.QueueMessage("\n", color);
                }
                else
                {
                    // Empty line
                    streaming.QueueMessage("\n", color);
                }
            }
        }
    }
}
