using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class WeatherCommand : BaseCommand
    {
        public override string Name => "weather";
        public override string Description => "Check the weather (in a humorous way)";
        public override string[] Aliases => new[] { "forecast", "temp" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, "bro just check outside your window gng\n", Color.Yellow);
            return Task.FromResult(true);
        }
    }
}
