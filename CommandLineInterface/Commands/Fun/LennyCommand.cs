using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class LennyCommand : BaseCommand
    {
        public override string Name => "lenny";
        public override string Description => "Display random Lenny face";
        public override string[] Aliases => new[] { "lennyface", "face" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var faces = new[] {
                "( ͡° ͜ʖ ͡°)", "( ͠° ͟ʖ ͡°)", "ᕦ( ͡° ͜ʖ ͡°)ᕤ", "( ͡~ ͜ʖ ͡°)",
                "( ͡ᵔ ͜ʖ ͡ᵔ )", "( ͡⊙ ͜ʖ ͡⊙)", "( ͡◉ ͜ʖ ͡◉)"
            };
            
            var random = new Random();
            var face = faces[random.Next(faces.Length)];
            AppendText(context, $" {face}\n", Color.Yellow);
            AppendText(context, " You know what that means...\n", Color.Gray);
            
            return Task.FromResult(true);
        }
    }
}
