using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class RickRollCommand : BaseCommand
    {
        public override string Name => "rickroll";
        public override string Description => "Get rickrolled in ASCII form";
        public override string[] Aliases => new[] { "rick", "roll", "never-gonna-give-you-up" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, "         ⠀⠀⠀⠀⣠⣶⡾⠏⠉⠙⠳⢦⡀⠀⠀⠀⢠⠞⠉⠙⠲⡀⠀\n", Color.Red);
            AppendText(context, "         ⠀⠀⠀⣴⠿⠏⠀⠀⠀⠀⠀⠀⢳⡀⠀⡏⠀⠀⠀⠀⠀⢷\n", Color.Red);
            AppendText(context, "         ⠀⠀⢠⣟⣋⡀⢀⣀⣀⡀⠀⣀⡀⣧⠀⢸⠀⠀⠀⠀⠀⠀⣿\n", Color.Red);
            AppendText(context, "         ⠀⠀⢸⣯⡭⠁⠸⣛⣟⠆⡴⣻⡲⣿⠀⣸ Never gonna give you up\n", Color.White);
            AppendText(context, "         ⠀⠀⣟⣿⡭⠀⠀⠀⠀⠀⢱⠀⠀⣿⠀⢹ Never gonna let you down\n", Color.White);
            AppendText(context, "         ⠀⠀⠙⢿⣯⠄⠀⠀⠀⢀⡀⠀⠀⡿⠀⠀⡇ Never gonna run around\n", Color.White);
            AppendText(context, "         ⠀⠀⠀⠀⠹⣶⠆⠀⠀⠀⠀⠀⡴⠃⠀⠀⠘⠤⣄ and desert you\n", Color.White);
            AppendText(context, " You just got rickrolled in ASCII form lol\n", Color.Yellow);
            
            return Task.FromResult(true);
        }
    }
}
