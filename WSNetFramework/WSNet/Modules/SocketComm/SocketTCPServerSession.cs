using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WSNet.Protocol;

namespace WSNet.Modules.SocketComm
{
    class SocketTCPServerClientSession{
        ClinetHandler handler;
        TcpClient tcpClient;
        NetworkStream stream;
        byte[] connectionToken;
        byte[] token;
        CancellationTokenSource cts = new CancellationTokenSource();
        string ip;
        int port;

        public SocketTCPServerClientSession(ClinetHandler handler, TcpClient tcpClient, byte[] connectionToken, byte[] token){
            this.handler = handler;
            this.tcpClient = tcpClient;
            this.connectionToken = connectionToken;
            this.token = token;
            this.stream = this.tcpClient.GetStream();
            this.ip = ((System.Net.IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
            this.port = ((System.Net.IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;
        }

        public void stop()
        {
            cts.Cancel();
            stream.Close();
            tcpClient.Close();
            
        }

        public async Task recv()
        {
            // first we signal that a new connection has been creatred
            CMDSRVSD notifyres = new CMDSRVSD(this.connectionToken, ip, port, new byte[0]);
            await handler.sendServerSocketData(token, notifyres);

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
                        byte[] datares = new byte[amountRead];
                        Array.Copy(data, datares, amountRead);
                        CMDSRVSD res = new CMDSRVSD(this.connectionToken, ip, port, datares);
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

        public async Task<bool> send(CMDSRVSD cmd)
        {
            try
            {
                if (!cts.IsCancellationRequested)
                {
                    await stream.WriteAsync(cmd.data, 0, cmd.data.Length, cts.Token).ConfigureAwait(false);
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
        ClinetHandler handler;

        public SocketTCPServerSession(CMDHeader cmdhdr, CMDConnect cmd, ClinetHandler handler)
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
                server_sock = new TcpListener(System.Net.IPAddress.Parse(ip), port) { ExclusiveAddressUse = false };
                server_sock.Start();
                await handler.sendContinue(token);
                _ = listenForClient();
                return true;

            }
            catch (Exception e)
            {
                WSNETDebug.Log("[TCPServer] Error creating listener! Reason: " + e.ToString());
                await handler.sendErr(token, "Generic error -TCP server- " + e.ToString());
                return false;
            }
        }

        private async Task listenForClient()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await server_sock.AcceptTcpClientAsync();
                    byte[] connectionToken = getConnectionToken();
                    SocketTCPServerClientSession session = new SocketTCPServerClientSession(handler, client, connectionToken, initiator_cmdhdr.token);
                    _ = session.recv();
                    string tokenhex = BitConverter.ToString(connectionToken);
                    sessions.Add(tokenhex, session);
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
            server_sock.Stop();
            foreach (var session in sessions)
            {
                session.Value.stop();
            }
        }



        public override async Task<bool> send(CMDHeader cmdhdr)
        {
            try{
                CMDSRVSD cmd = (CMDSRVSD)cmdhdr.packet;
                string tokenhex = BitConverter.ToString(cmd.connectiontoken);
                var session = sessions[tokenhex];
                return await session.send(cmd);
            }
            catch (Exception e)
            {
                await handler.sendErr(token, "Socket send error " + e.ToString());
                return false;
            }
        }

        
    }
}
