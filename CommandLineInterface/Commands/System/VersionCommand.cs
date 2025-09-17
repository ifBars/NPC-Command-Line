using CommandLineInterface.Core;
using NPC_Terminal.Properties;

namespace CommandLineInterface.Commands.System
{
    public class VersionCommand : BaseCommand
    {
        public override string Name => "version";
        public override string Description => "Show current program version";

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, " NPC Terminal", Color.MediumSpringGreen);
            AppendText(context, $" v.{Settings.Default.Version}!\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
