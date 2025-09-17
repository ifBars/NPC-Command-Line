using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class UuidCommand : BaseCommand
    {
        public override string Name => "uuid";
        public override string Description => "Generate a new UUID/GUID";
        public override string[] Aliases => new[] { "guid", "id" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var guid = Guid.NewGuid().ToString();
            AppendText(context, $" Fresh UUID: {guid}\n", Color.Cyan);
            
            return Task.FromResult(true);
        }
    }
}
