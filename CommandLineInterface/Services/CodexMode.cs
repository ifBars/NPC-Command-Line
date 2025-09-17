using CommandLineInterface.Core;

namespace CommandLineInterface.Services
{
    public class CodexMode : ITerminalMode
    {
        private CodexSession? session;

        public string Name => "codex";
        public bool IsActive => session?.IsActive == true;

        public async Task EnterAsync(TerminalContext context)
        {
            session = new CodexSession(context.CodexService, context.Append, context.GetWorkspaceRoot());
            await session.StartAsync();
        }

        public Task ExitAsync(TerminalContext context)
        {
            session?.Stop();
            return Task.CompletedTask;
        }

        public async Task<bool> HandleAsync(string input, TerminalContext context)
        {
            if (session == null) return false;

            var text = input.Trim();

            // Slash-prefixed management commands are handled here, never forwarded to LLM
            if (text.StartsWith("/", StringComparison.Ordinal))
            {
                await session.HandleUserInputAsync(text, CancellationToken.None);
                return true;
            }

            // Regular chat
            await session.HandleUserInputAsync(text, CancellationToken.None);
            return true;
        }
    }
}


