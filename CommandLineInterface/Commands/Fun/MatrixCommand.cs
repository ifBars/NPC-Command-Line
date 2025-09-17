using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class MatrixCommand : BaseCommand
    {
        public override string Name => "matrix";
        public override string Description => "Enter the Matrix with digital rain";
        public override string[] Aliases => new[] { "neo", "digital-rain" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ｦｱｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃ";
            var random = new Random();
            
            for (int i = 0; i < 15; i++)
            {
                var line = "";
                for (int j = 0; j < 50; j++)
                {
                    if (random.Next(0, 4) == 0)
                        line += chars[random.Next(chars.Length)];
                    else
                        line += " ";
                }
                AppendText(context, $" {line}\n", Color.LimeGreen);
            }
            AppendText(context, " Welcome to the Matrix, Neo\n", Color.Red);
            
            return Task.FromResult(true);
        }
    }
}
