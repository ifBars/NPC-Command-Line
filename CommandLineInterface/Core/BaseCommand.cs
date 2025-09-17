using CommandLineInterface.Core;

namespace CommandLineInterface.Core
{
    /// <summary>
    /// Base class for all terminal commands providing common functionality
    /// </summary>
    public abstract class BaseCommand : ICommand
    {
        public abstract string Name { get; }
        public abstract string Description { get; }

        /// <summary>
        /// Additional aliases for this command (optional)
        /// </summary>
        public virtual string[] Aliases => Array.Empty<string>();

        public abstract Task<bool> ExecuteAsync(string arguments, TerminalContext context);

        /// <summary>
        /// Helper method to append colored text to the terminal
        /// </summary>
        protected void AppendText(TerminalContext context, string text, Color color)
        {
            context.Append(text, color);
        }

        /// <summary>
        /// Helper method to append regular white text
        /// </summary>
        protected void AppendText(TerminalContext context, string text)
        {
            context.Append(text, Color.White);
        }

        /// <summary>
        /// Helper method to show usage information
        /// </summary>
        protected void ShowUsage(TerminalContext context, string usage)
        {
            context.Append($" Usage: {usage}\n", Color.Yellow);
        }

        /// <summary>
        /// Helper method to show error messages
        /// </summary>
        protected void ShowError(TerminalContext context, string error)
        {
            context.Append($" Error: {error}\n", Color.Red);
        }
    }
}
