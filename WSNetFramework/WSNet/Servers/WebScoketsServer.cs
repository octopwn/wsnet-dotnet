using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Fleck;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Diagnostics;

namespace WSNet.Servers.WebSocket
{
    class WebSocketTransport : Transport
    {
        public IWebSocketConnection websocket;

        public WebSocketTransport(IWebSocketConnection websocket)
        {
            this.websocket = websocket;
        }

        public override async Task Send(byte[] data)
        {
            await websocket.Send(data);
        }
    }

    class WebSocketsServer : IDisposable
    {
        string ip;
        int port;
        bool secureURL;
        string pfx_file = "";
        string pfx_password = "";
        Dictionary<Guid, ClinetHandler> clientLookup;

        CancellationTokenSource cts;

        static public List<string> ListIPAddresses()
        {
            var list = new List<string>();
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    var ipProperties = networkInterface.GetIPProperties();

                    foreach (var address in ipProperties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            list.Add(address.Address.ToString());
                        }
                    }
                }
            }
            return list;
        }

        public WebSocketsServer(string ip, int port, bool secureURL = false)
        {
            this.ip = ip;
            this.port = port;
            this.secureURL = secureURL;
            clientLookup = new Dictionary<Guid, ClinetHandler>();
        }

        public WebSocketsServer(string ip, int port, string pfx_file, string pfx_password, bool secureURL = false)
        {
            this.ip = ip;
            this.port = port;
            this.pfx_password = pfx_file;
            this.pfx_file = pfx_file;
            clientLookup = new Dictionary<Guid, ClinetHandler>();
            this.secureURL = secureURL;
        }

        public async Task Run()
        {
            FleckLog.LogAction = (level, message, ex) => { }; //disable logging. If you want logs, comment out this line.
            cts = new CancellationTokenSource();

            FleckLog.Level = LogLevel.Debug;
            string proto = "ws://";
            if (pfx_file.Length != 0)
            {
                proto = "wss://";
            }

            var uuid = "";
            if (secureURL)
            {
                uuid = Guid.NewGuid().ToString();
            }
            var websocketUrl = $"{proto}{ip}:{port}/{uuid}";

            var server = new WebSocketServer(websocketUrl, false);
            if (pfx_file.Length != 0)
            {
                server.Certificate = new X509Certificate2(pfx_file, pfx_password);
            }
            server.Start(socket =>
            {
                if (socket.ConnectionInfo.Path == $"/{uuid}")
                {
                    socket.OnOpen = () =>
                    {
                        WebSocketTransport transport = new WebSocketTransport(socket);
                        ClinetHandler ch = new ClinetHandler();
                        Task.Run(() => ch.run(transport));
                        clientLookup.Add(socket.ConnectionInfo.Id, ch);
                    };
                    socket.OnClose = () =>
                    {
                        if (clientLookup.ContainsKey(socket.ConnectionInfo.Id))
                        {
                            clientLookup[socket.ConnectionInfo.Id].Stop();
                            clientLookup.Remove(socket.ConnectionInfo.Id);
                        }

                    };
                    socket.OnMessage = message =>
                    {
                        WSNETDebug.Log("String message is unexpected here!");
                    };
                    socket.OnBinary = message =>
                    {
                        if (clientLookup.ContainsKey(socket.ConnectionInfo.Id))
                            _ = clientLookup[socket.ConnectionInfo.Id].processMessage(message);
                    };
                }
                else
                {
                    // Reject the connection if the path does not match
                    Console.WriteLine($"Invalid connection attempt: {socket.ConnectionInfo.Path}");
                    socket.Close();
                }
            });
            
            if(ip == "0.0.0.0")
            {
                List<string> allinterfaces = new List<string>();
                foreach (string otherip in ListIPAddresses())
                {
                    Console.WriteLine($"{proto}{otherip}:{port}/{uuid}");
                }
            }
            else
            {
                Console.WriteLine(websocketUrl);
            }
            await Task.Delay(Timeout.Infinite);
        }

        public void Stop()
        {
            foreach (Guid socketid in clientLookup.Keys)
            {
                clientLookup[socketid].Stop();
            }

        }

        public void Dispose()
        {
            Stop();
            clientLookup = new Dictionary<Guid, ClinetHandler>();
        }       

        static public void StartMain(string[] args)
        {
            string ip = "127.0.0.1";
            int port = 8700;
            if (args.Length == 1)
            {
                if (args[0].ToLower() == "-h" || args[0].ToLower() == "--help" || args[0].ToLower() == "h" || args[0].ToLower() == "help")
                {
                    Console.WriteLine("WSNET websockets2TCP proxy.");
                    Console.WriteLine("Usage: proxy.exe <listen_ip> <listen_port> <PFX file> <PFX password>");
                    Console.WriteLine("PFX file and password only needed if you want a WS+SSL server");
                    Console.WriteLine("Default values: listen_ip=127.0.0.1 listen_port=8100 no SSL");
                    return;
                }
                ip = args[0];
            }
            if (args.Length >= 2)
            {
                ip = args[0];
                port = int.Parse(args[1]);
            }
            if (args.Length >= 4)
            {
                ip = args[0];
                port = int.Parse(args[1]);
            }

            string pfx_file = "";
            string pfx_password = "";

            using (WebSocketsServer wss = new WebSocketsServer(ip, port, pfx_file, pfx_password, true))
            {
                var task = wss.Run();
                task.Wait();
            }

        }
    }
}
