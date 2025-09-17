using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.System
{
    public class SystemInfoCommand : BaseCommand
    {
        public override string Name => "sys";
        public override string Description => "Show system information";
        public override string[] Aliases => new[] { "system", "sysinfo", "info" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            AppendText(context, $" OS: {Environment.OSVersion}\n", Color.White);
            AppendText(context, $" Machine: {Environment.MachineName}\n", Color.White);
            AppendText(context, $" User: {Environment.UserName}\n", Color.White);
            AppendText(context, $" Processors: {Environment.ProcessorCount}\n", Color.White);
            AppendText(context, $" RAM: {GC.GetTotalMemory(false) / 1024 / 1024} MB used by this app\n", Color.White);
            
            return Task.FromResult(true);
        }
    }
}
