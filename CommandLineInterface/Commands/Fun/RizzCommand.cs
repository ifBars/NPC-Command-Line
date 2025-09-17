using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Fun
{
    public class RizzCommand : BaseCommand
    {
        public override string Name => "rizz";
        public override string Description => "Check your rizz level";
        public override string[] Aliases => new[] { "charisma", "charm" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var rizzLevel = new Random().Next(1, 101);
            AppendText(context, $"    📊 RIZZ LEVEL: {rizzLevel}% 📊\n", Color.Pink);

            if (rizzLevel < 30)
                AppendText(context, " Negative rizz detected 💀\n", Color.Red);
            else if (rizzLevel < 60)
                AppendText(context, " Mid rizz energy ⚡\n", Color.Yellow);
            else if (rizzLevel < 85)
                AppendText(context, " Solid rizz game 🔥\n", Color.Orange);
            else
                AppendText(context, " UNSPOKEN RIZZ 👑\n", Color.Gold);

            var tips = new[] { "be yourself", "confidence is key", "respectful vibes only", "genuine conversation wins" };
            var random = new Random();
            AppendText(context, $" Pro tip: {tips[random.Next(tips.Length)]}\n", Color.LightBlue);
            
            return Task.FromResult(true);
        }
    }
}
