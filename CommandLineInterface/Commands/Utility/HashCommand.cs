using CommandLineInterface.Core;
using System.Security.Cryptography;
using System.Text;

namespace CommandLineInterface.Commands.Utility
{
    public class HashCommand : BaseCommand
    {
        public override string Name => "hash";
        public override string Description => "Generate MD5 hash of text";
        public override string[] Aliases => new[] { "md5", "checksum" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                ShowUsage(context, "hash <text>");
                return Task.FromResult(true);
            }

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(arguments));
            var hashString = Convert.ToHexString(hash).ToLower();
            AppendText(context, $" MD5: {hashString}\n", Color.LightBlue);
            
            return Task.FromResult(true);
        }
    }
}
