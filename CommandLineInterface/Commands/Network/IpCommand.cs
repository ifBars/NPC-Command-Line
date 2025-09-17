using CommandLineInterface.Core;

namespace CommandLineInterface.Commands.Network
{
    public class IpCommand : BaseCommand
    {
        public override string Name => "ip";
        public override string Description => "Show local and public IP addresses";
        public override string[] Aliases => new[] { "ipaddress", "myip", "whatsmyip" };

        public override async Task<bool> ExecuteAsync(string arguments, TerminalContext context)
        {
            try
            {
                var host = global::System.Net.Dns.GetHostEntry(global::System.Net.Dns.GetHostName());
                var localIP = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == global::System.Net.Sockets.AddressFamily.InterNetwork);
                AppendText(context, $" Local IP: {localIP}\n", Color.LightGreen);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var publicIP = await client.GetStringAsync("https://api.ipify.org");
                AppendText(context, $" Public IP: {publicIP.Trim()}\n", Color.LightBlue);
            }
            catch (HttpRequestException)
            {
                AppendText(context, " service down bruv \n", Color.Red);
            }
            catch (TaskCanceledException)
            {
                AppendText(context, " request timed out\n", Color.Red);
            }
            catch
            {
                AppendText(context, " Network said NUH UH\n", Color.Red);
            }
            
            return true;
        }
    }
}
