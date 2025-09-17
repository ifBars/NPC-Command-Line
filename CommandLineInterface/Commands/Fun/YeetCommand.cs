using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class YeetCommand : BaseCommand
    {
        public override string Name => "yeet";
        public override string Description => "Maximum power achieved!";
        public override string[] Aliases => new[] { "throw", "launch" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var message = string.IsNullOrEmpty(arguments) ? "YEET!" : arguments;
            
            AppendText(context, "    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⡠⠤⠖⠒⠋⠉⠉⠉⠉⠓⠲⢤⡀⠀⠀⠀⠀⠀\n", Color.Yellow);
            AppendText(context, "    ⠀⠀⠀⠀⠀⠀⠀⠀⠀⢀⡴⠊⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠙⢦⡀⠀⠀⠀\n", Color.Yellow);
            AppendText(context, "    ⠀⠀⠀⠀⠀⠀⠀⠀⡰⠋⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠙⢆⠀⠀\n", Color.Yellow);
            AppendText(context, "    ⠀⠀⠀⠀⠀⠀⠀⡼⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⢧⠀⠀\n", Color.Yellow);
            AppendText(context, $"   Y E E T ! ! !   {message}\n", Color.Red);
            AppendText(context, " Maximum power achieved! 🚀\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
