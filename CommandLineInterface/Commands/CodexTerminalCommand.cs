using CommandLineInterface.Core;

namespace CommandLineInterface.Commands
{
    public class CodexTerminalCommand : ICommand
    {
        public string Name => "codex";
        public string Description => "Enter Codex mode (LLM coding assistant)";

        public async Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            if (context.CurrentMode is Services.CodexMode codex && codex.IsActive)
            {
                await codex.ExitAsync(context);
                context.Append(" CODEX mode exited.\n", Color.White);
                context.CurrentMode = null;
                return true;
            }

            var mode = new Services.CodexMode();
            context.CurrentMode = mode;
            await mode.EnterAsync(context);
            return true;
        }
    }
}


