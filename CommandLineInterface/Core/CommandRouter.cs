namespace CommandLineInterface.Core
{
    public class CommandRouter
    {
        private readonly Dictionary<string, ICommand> commands = new(StringComparer.OrdinalIgnoreCase);

        public void Register(ICommand command)
        {
            commands[command.Name] = command;
        }

        public async Task<bool> TryRouteAsync(string input, TerminalContext context)
        {
            if (string.IsNullOrWhiteSpace(input)) return true;

            var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var name = parts[0];
            var args = parts.Length > 1 ? parts[1] : string.Empty;

            if (context.CurrentMode != null && context.CurrentMode.IsActive)
            {
                var handled = await context.CurrentMode.HandleAsync(input, context);
                return handled;
            }

            if (commands.TryGetValue(name, out var cmd))
            {
                return await cmd.ExecuteAsync(args, context);
            }

            return false;
        }
    }
}


