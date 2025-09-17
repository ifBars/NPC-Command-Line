using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Utility
{
    public class CalculatorCommand : BaseCommand
    {
        public override string Name => "calc";
        public override string Description => "Calculate mathematical expressions";
        public override string[] Aliases => new[] { "calculator", "math" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                ShowUsage(context, "calc <expression> (like calc 2+2)");
                return Task.FromResult(true);
            }

            try
            {
                var table = new global::System.Data.DataTable();
                var result = table.Compute(arguments, null);
                AppendText(context, $" {arguments} = {result}\n", Color.LightGreen);
            }
            catch
            {
                AppendText(context, " That math ain't mathing chief\n", Color.Red);
            }
            
            return Task.FromResult(true);
        }
    }
}
