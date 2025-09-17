using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class TimeCommand : BaseCommand
    {
        public override string Name => "time";
        public override string Description => "Show current date and time";

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var now = DateTime.Now;
            AppendText(context, $" Current time: {now:HH:mm:ss}\n Date: {now:dddd, MMMM dd, yyyy}\n", Color.Cyan);
            
            return Task.FromResult(true);
        }
    }
}
