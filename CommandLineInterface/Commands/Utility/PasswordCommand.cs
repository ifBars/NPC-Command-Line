using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class PasswordCommand : BaseCommand
    {
        public override string Name => "password";
        public override string Description => "Generate a secure random password";
        public override string[] Aliases => new[] { "pwd", "pass", "gen-password" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            var length = 16;
            
            if (!string.IsNullOrEmpty(arguments) && int.TryParse(arguments, out int customLength))
                length = Math.Min(Math.Max(customLength, 4), 128);
                
            var password = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
            AppendText(context, $" gen password: {password}\n", Color.LightGreen);
            
            return Task.FromResult(true);
        }
    }
}
