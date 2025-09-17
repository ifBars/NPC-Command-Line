using CommandLineInterface.Core;
using System.Text;

namespace CommandLineInterface.Commands.Utility
{
    public class EncodeCommand : BaseCommand
    {
        public override string Name => "encode";
        public override string Description => "Encode text to Base64";
        public override string[] Aliases => new[] { "base64", "b64" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                ShowUsage(context, "encode <text>");
                return Task.FromResult(true);
            }

            var bytes = Encoding.UTF8.GetBytes(arguments);
            var encoded = Convert.ToBase64String(bytes);
            AppendText(context, $" Base64: {encoded}\n", Color.LightBlue);
            
            return Task.FromResult(true);
        }
    }
}
