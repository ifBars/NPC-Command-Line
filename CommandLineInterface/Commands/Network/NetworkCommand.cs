using CommandLineInterface.Core;
using System.Net.NetworkInformation;

namespace CommandLineInterface.Commands.Network
{
    public class NetworkCommand : BaseCommand
    {
        public override string Name => "network";
        public override string Description => "Test network connectivity";
        public override string[] Aliases => new[] { "ping", "net", "connectivity" };

        public override Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            try
            {
                var ping = new Ping();
                var reply = ping.Send("8.8.8.8", 1000);
                if (reply.Status == IPStatus.Success)
                {
                    AppendText(context, $" Google DNS ping: {reply.RoundtripTime}ms\n", Color.LightGreen);
                }
                else
                {
                    AppendText(context, " Network might be dead for real\n", Color.Red);
                }
            }
            catch
            {
                AppendText(context, " Bro dont have internet access\n", Color.Red);
            }
            
            return Task.FromResult(true);
        }
    }
}
