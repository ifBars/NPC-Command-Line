using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class NyanCommand : BaseCommand
    {
        public override string Name => "nyan";
        public override string Description => "Nyan Cat ASCII art";
        public override string[] Aliases => new[] { "nyancat", "cat", "rainbow" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, " ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░\n", Color.Gray);
            AppendText(context, " ░░░░░░░░░░▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄░░░░░░░\n", Color.Gray);
            AppendText(context, " ░░░░░░░░▄▀░░░░░░░░░░░░▄░░░░░░░▀▄░░░░░░\n", Color.Gray);
            AppendText(context, " ░░░░░░░░█░░▄░░░░▄░░░░░░░░▄░░░░░█░░░░░░\n", Color.Gray);
            AppendText(context, " ░░░░░░░░█░░░░░░░░░░▄█▄▄░░▄░░░░░█░▄▄▄░░\n", Color.Gray);
            AppendText(context, " ░▄▄▄▄▄░░█░░░░░░▀░░░░▀█░░░░░░░░█▀▀░██░░\n", Color.Gray);
            AppendText(context, " ░██▄▀██▄█░░░▄░░░░░░░██░░░░▄░░░█░░░░▀▀░░\n", Color.Gray);
            AppendText(context, " ░░▀██▄▀██░░░░░░░░▀░██▀░░░░░░░░█░░░░░░░░\n", Color.Gray);
            AppendText(context, " ░░░░▀████░▀░░░░▄░░░██░░░▄░░░░▄█░░░░░░░░\n", Color.Gray);
            AppendText(context, " ~=[,,_,,]:3 NYAN NYAN NYAN!\n", Color.Magenta);
            
            return Task.FromResult(true);
        }
    }
}
