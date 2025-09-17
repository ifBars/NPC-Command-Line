using CommandLineInterface.Core;
using System.Diagnostics;

namespace CommandLineInterface.Commands.Configuration
{
    public class AliasCommand : BaseCommand
    {
        public override string Name => "alias";
        public override string Description => "Open alias configuration file in notepad";
        public override string[] Aliases => new[] { "createalias", "aliases" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            try
            {
                var notepad = new Process();
                notepad.StartInfo = new ProcessStartInfo("notepad.exe", Path.Combine(Application.StartupPath, "aliases.txt"));
                notepad.EnableRaisingEvents = true;
                notepad.Exited += (s, e) => { context.Form.Invoke(() => { context.Form.LoadAliasesFromFile(); }); };
                notepad.Start();
            }
            catch (Exception e)
            {
                ShowError(context, "Error opening the alias file. Please report it:");
                AppendText(context, $" Error: {e}\n", Color.Yellow);
            }
            
            return Task.FromResult(true);
        }
    }
}
