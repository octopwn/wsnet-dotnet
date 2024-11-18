using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WSNet;

namespace WSNetFramework.WSNet.Modules.SocketComm
{
    internal class DNSResolver
    {
        public static async Task<IPHostEntry> GetHostEntryWithTimeout(string ipOrHostname, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                var dnsTask = Dns.GetHostEntryAsync(ipOrHostname);

                var timeoutTask = Task.Delay(timeout, cts.Token);

                var completedTask = await Task.WhenAny(dnsTask, timeoutTask);

                if (completedTask == dnsTask)
                {
                    cts.Cancel(); // Cancel the timeout task to release resources
                    return await dnsTask; // Await the DNS task to propagate exceptions if any
                }

                throw new TimeoutException("DNS resolution timed out.");
            }
        }

        public static async Task<List<string>> ResolveAsync(List<string> ipOrHostnames, int timeout = 1)
        {
            // Define a list of tasks, each resolving a hostname or IP
            var tasks = ipOrHostnames.Select(async ipOrHostname =>
            {
                bool isIP = ParseUtils.IsIpAddress(ipOrHostname);
                try
                {
                    IPHostEntry res = await DNSResolver.GetHostEntryWithTimeout(ipOrHostname, TimeSpan.FromSeconds(timeout));
                    return isIP
                        ? res.HostName // Return the hostname if input is an IP
                        : string.Join(", ", res.AddressList.Select(ip => ip.ToString())); // Return IPs if input is a hostname
                }
                catch
                {
                    return ""; // Return an empty string on failure
                }
            }).ToList();

            // Wait for all tasks to complete while keeping the order
            return (await Task.WhenAll(tasks)).ToList();
        }
    }
}
