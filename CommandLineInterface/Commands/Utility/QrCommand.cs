using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class QrCommand : BaseCommand
    {
        public override string Name => "qr";
        public override string Description => "Generate QR code (in your imagination)";
        public override string[] Aliases => new[] { "qrcode", "qr-code" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                ShowUsage(context, "qr <text to encode>");
                return Task.FromResult(true);
            }
            
            AppendText(context, " QR code generated in your imagination because this is a terminal\n", Color.Magenta);
            AppendText(context, $" Text: {arguments}\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
