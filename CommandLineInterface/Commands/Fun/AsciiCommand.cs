using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class AsciiCommand : BaseCommand
    {
        public override string Name => "ascii";
        public override string Description => "Create ASCII art box with text";
        public override string[] Aliases => new[] { "box", "frame" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                ShowUsage(context, "ascii <text>");
                return Task.FromResult(true);
            }
            
            AppendText(context, $" ╔══════════════════════════╗\n", Color.Cyan);
            AppendText(context, $" ║  {arguments.PadRight(20)}  ║\n", Color.Cyan);
            AppendText(context, $" ╚══════════════════════════╝\n", Color.Cyan);
            AppendText(context, " There's your fancy ASCII box mate\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
