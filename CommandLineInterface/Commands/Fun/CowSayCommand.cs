using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class CowSayCommand : BaseCommand
    {
        public override string Name => "cowsay";
        public override string Description => "Make a cow say something";
        public override string[] Aliases => new[] { "cow", "moo" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var message = string.IsNullOrEmpty(arguments) ? "Moo!" : arguments;
            var border = new string('-', message.Length + 2);
            
            AppendText(context, $" {border}\n", Color.White);
            AppendText(context, $"< {message} >\n", Color.White);
            AppendText(context, $" {border}\n", Color.White);
            AppendText(context, "        \\   ^__^\n", Color.White);
            AppendText(context, "         \\  (oo)\\_______\n", Color.White);
            AppendText(context, "            (__)\\       )\\/\\\n", Color.White);
            AppendText(context, "                ||----w |\n", Color.White);
            AppendText(context, "                ||     ||\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
