using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.System
{
    public class CodeCommand : BaseCommand
    {
        public override string Name => "code";
        public override string Description => "Show where to find the source code";
        public override string[] Aliases => new[] { "github", "source", "repo" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, " NPC Terminal", Color.MediumSpringGreen);
            AppendText(context, " code available at github.com/CsharpProgramming/NPC-Command-Line\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
