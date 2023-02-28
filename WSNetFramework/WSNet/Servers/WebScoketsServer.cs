using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Fleck;

namespace WSNet.Servers
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

    class WebSocketsServer
    {
        string ip;
        int port;
        string pfx_file = "";
        string pfx_password = "";

        CancellationTokenSource cts;

        public WebSocketsServer(string ip, int port)
        {
            this.ip = ip;
            this.port = port;
        }

        public WebSocketsServer(string ip, int port, string pfx_file, string pfx_password)
        {
            this.ip = ip;
            this.port = port;
            this.pfx_password = pfx_file;
            this.pfx_file = pfx_file;

        }

        public async Task Run()
        {
            FleckLog.LogAction = (level, message, ex) => { }; //disable logging. If you want logs, comment out this line.
            cts = new CancellationTokenSource();
            Dictionary<Guid, WSNetClinetHandler> clientLookup = new Dictionary<Guid, WSNetClinetHandler>();

            FleckLog.Level = LogLevel.Debug;
            string proto = "ws://";
            if (pfx_file.Length != 0)
            {
                proto = "wss://";
            }


            var server = new WebSocketServer(proto + ip + ":" + port.ToString(), false);
            if (pfx_file.Length != 0)
            {
                server.Certificate = new X509Certificate2(pfx_file, pfx_password);
            }
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    WebSocketTransport transport = new WebSocketTransport(socket);
                    WSNetClinetHandler ch = new WSNetClinetHandler();
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
                    Console.WriteLine("String message is unexpected here!");
                };
                socket.OnBinary = message =>
                {
                    if (clientLookup.ContainsKey(socket.ConnectionInfo.Id))
                        clientLookup[socket.ConnectionInfo.Id].processMessage(message);
                };
            });

            var input = Console.ReadLine();
            while (input != "exit")
            {
                foreach (Guid socketid in clientLookup.Keys)
                {
                    clientLookup[socketid].Stop();
                }
            }
        }
    }
}
