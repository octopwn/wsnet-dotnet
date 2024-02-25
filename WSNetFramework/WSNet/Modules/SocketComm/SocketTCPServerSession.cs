using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WSNet.SocketComm
{
    class SocketTCPServerClientSession{
        WSNetClinetHandler handler;
        TcpClient tcpClient;
        byte[] connectionToken;
        byte[] token;
        CancellationTokenSource cts = new CancellationTokenSource();
        string ip;

        public SocketTCPServerClientSession(WSNetClinetHandler handler, TcpClient tcpClient, byte[] connectionToken, byte[] token){
            this.handler = handler;
            this.tcpClient = tcpClient;
            this.connectionToken = connectionToken;
            this.token = token;
            this.stream = this.tcpClient.GetStream();
            this.ip = ((System.Net.IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
        }

        public override void stop()
        {
            cts.Cancel();
            this.stream.Close();
            client_sock.Close();
            
        }

        public async Task recv()
        {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        byte[] data = new byte[65000];
                        int amountRead = await stream.ReadAsync(data, 0, data.Length, cts.Token);
                        if (amountRead == 0)
                        {
                            throw new Exception("Socket terminated");
                        }
                        byte[] res = new byte[amountRead];
                        Array.Copy(data, res, amountRead);
                        CMDSRVSD res = new CMDSRVSD(this.connectionToken, ip, 0, res);
                        await handler.sendServerSocketData(token, res);
                    }
                    catch (Exception e)
                    {
                        stop();
                        await handler.sendErr(token, "Socket Recv error! " + e.ToString());
                        return;
                    }
                }
        }

        public override async Task<bool> send(CMDHeader cmdhdr)
        {
            try
            {
                if (!cts.IsCancellationRequested)
                {
                    await stream.WriteAsync(cmdhdr.data, 0, cmdhdr.data.Length, cts.Token).ConfigureAwait(false);
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Socket send Error! " + e.Message);
                stop();
                await handler.sendErr(token, "Socket send error " + e.ToString());
                return false;
            }
        }

    }

    class SocketTCPServerSession : SocketServerSession
    {
        string ip;
        int port;
        TcpListener server_sock;
        byte[] token;
        CancellationTokenSource cts = new CancellationTokenSource();
        Dictionary<string, SocketTCPServerClientSession> sessions = new Dictionary<string, SocketTCPServerClientSession>();
        WSNetClinetHandler handler;

        public SocketTCPServerSession(CMDHeader cmdhdr, CMDConnect cmd, WSNetClinetHandler handler)
        {
            this.initiator_cmdhdr = cmdhdr;
            this.initiator_cmd = cmd;
            this.ip = cmd.ip;
            this.port = cmd.port;
            this.token = cmdhdr.token;
            this.handler = handler;
        }

        private byte[] getConnectionToken(){
            // return random guuid as byte array
            return Guid.NewGuid().ToByteArray();
        } 

        public async Task<bool> serve()
        {
            try
            {
                server_sock = new TcpListener(System.Net.IPAddress.Parse(ip), port);
                server_sock.Start();
                listenForClient();
                return true;
            }
            catch (Exception e)
            {
                await handler.sendErr(token, "Generic error -sockst connect- " + e.ToString());
                return false;
            }
        }

        private async listenForClient()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await server_sock.AcceptTcpClientAsync();
                    SocketTCPServerSession session = new SocketTCPServerSession(initiator_cmdhdr, initiator_cmd, handler);
                    session.client_sock = client;
                    session.token = getConnectionToken();
                    await handler.sendSocketSession(session);
                    session.recv();
                    sessions.Add(session.token, session);
                }
                catch (Exception e)
                {
                    stop();
                    await handler.sendErr(token, "Socket accept error " + e.ToString());
                    return;
                }
            }
        }

        public override void stop()
        {
            cts.Cancel();
            client_sock.Close();
            foreach (var session in sessions)
            {
                session.Value.stop();
            }
        }



        public override async Task<bool> send(CMDHeader cmdhdr)
        {
            try{
                CMDSRVSD cmd = CMDSRVSD.parse(cmdhdr.data);
                var session = sessions[cmd.connectionToken];
                return await session.send(cmd);
            }
            catch (Exception e)
            {
                await handler.sendErr(token, "Socket send error " + e.ToString());
                return false;
            },
        }

        
    }
}
