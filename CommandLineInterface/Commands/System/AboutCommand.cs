using CommandLineInterface.Core;
using NPC_Terminal.Properties;

namespace CommandLineInterface.Commands.System
{
    public class AboutCommand : BaseCommand
    {
        public override string Name => "about";
        public override string Description => "Information about NPC Terminal and its features";

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, " Welcome to ", Color.White);
            AppendText(context, "NPC Terminal", Color.MediumSpringGreen);
            AppendText(context, $" v.{Settings.Default.Version}!\n", Color.White);
            AppendText(context, " NPC Terminal is a clone of Windows Terminal but better. With Quality of Life features, better design, and more.\n Supports adding custom commands. Made by youtube.com/@CsharpProgramming\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
