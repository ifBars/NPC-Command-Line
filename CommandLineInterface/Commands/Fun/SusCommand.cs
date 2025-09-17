using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class SusCommand : BaseCommand
    {
        public override string Name => "sus";
        public override string Description => "Display sus meter with Among Us ASCII art";

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, "      ⠀⠀⠀⠀⠀⢀⣴⡾⠿⠿⠿⠿⢶⣦⣄⠀⠀⠀\n", Color.Red);
            AppendText(context, "      ⠀⠀⠀⠀⢠⣿⠁⠀⠀⠀⣀⣀⣀⣈⣻⣷⡄⠀\n", Color.Red);
            AppendText(context, "      ⠀⠀⠀⠀⣾⡇⠀⠀⣾⣟⠛⠋⠉⠉⠙⠛⢷⣄⠀\n", Color.Red);
            AppendText(context, "      ⠀⠀⠀⠀⣿⠀⠀⠀⠶⠿⠭⠭⠭⠭⠭⠭⠭⠬⠭⠀\n", Color.Red);
            AppendText(context, "      ⠀⠀⠀⠀⣿⣷⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀\n", Color.Red);
            AppendText(context, "      ⠀⠀⠀⠀⢿⣿⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀\n", Color.Red);
            AppendText(context, "      ⠀⠀⠀⠀⠀⠿⣷⣶⣤⣤⣤⣤⣭⣭⣭⣭⣭⣭⡀\n", Color.Red);
            
            var suspiciousness = new Random().Next(0, 101);
            AppendText(context, $" That's pretty sus ngl... Suspicion level: {suspiciousness}%\n", Color.Yellow);
            if (suspiciousness > 75) 
                AppendText(context, " EMERGENCY MEETING!\n", Color.Red);
            
            return Task.FromResult(true);
        }
    }
}
