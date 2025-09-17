using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class SigmaCommand : BaseCommand
    {
        public override string Name => "sigma";
        public override string Description => "Display sigma grindset motivation";
        public override string[] Aliases => new[] { "grind", "mindset", "alpha" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var quotes = new[] {
                "keep grinding king the bag don't stop",
                "we don't do mid around here",
                "hustle hits different when you're sigma",
                "success or nothing bro no in between",
                "grind now flex later that's the move",
                "different breed mentality fr",
                "alpha energy only we don't settle"
            };
            
            var random = new Random();
            var quote = quotes[random.Next(quotes.Length)];
            AppendText(context, $" {quote}\n", Color.Gold);
            AppendText(context, " sigma rule #47: never stop the grind\n", Color.Gray);
            
            return Task.FromResult(true);
        }
    }
}
