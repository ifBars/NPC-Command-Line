using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class DogeCommand : BaseCommand
    {
        public override string Name => "doge";
        public override string Description => "Much wow, such terminal";
        public override string[] Aliases => new[] { "wow", "much", "such" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, "                       ▄              ▄\n", Color.Yellow);
            AppendText(context, "                      ▌▒█           ▄▀▒▌\n", Color.Yellow);
            AppendText(context, "                      ▌▒▒█        ▄▀▒▒▒▐\n", Color.Yellow);
            AppendText(context, "                     ▐▄▀▒▒▀▀▀▀▄▄▄▀▒▒▒▒▒▐\n", Color.Yellow);
            AppendText(context, "                   ▄▄▀▒░▒▒▒▒▒▒▒▒▒█▒▒▄█▒▐\n", Color.Yellow);
            AppendText(context, "                 ▄▀▒▒▒░░░▒▒▒░░░▒▒▒▀██▀▒▌\n", Color.Yellow);
            AppendText(context, "                ▐▒▒▒▄▄▒▒▒▒░░░▒▒▒▒▒▒▒▀▄▒▒▌\n", Color.Yellow);
            
            var phrases = new[] { "much terminal", "so command", "very code", "wow technology", "such programming", "many features" };
            var random = new Random();
            for (int i = 0; i < 3; i++)
            {
                AppendText(context, $" {phrases[random.Next(phrases.Length)]}\n", Color.Cyan);
            }
            
            return Task.FromResult(true);
        }
    }
}
