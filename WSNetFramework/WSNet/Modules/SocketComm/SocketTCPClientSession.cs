using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WSNet.SocketComm
{
    class SocketTCPClientSession : SocketSession
    {
        string ip;
        int port;
        TcpClient client_sock = new TcpClient();
        byte[] token;
        CancellationTokenSource cts = new CancellationTokenSource();
        NetworkStream stream;
        WSNetClinetHandler handler;

        public SocketTCPClientSession(CMDHeader cmdhdr, CMDConnect cmd, WSNetClinetHandler handler)
        {
            this.initiator_cmdhdr = cmdhdr;
            this.initiator_cmd = cmd;
            this.ip = cmd.ip;
            this.port = cmd.port;
            this.token = cmdhdr.token;
            this.handler = handler;
        }

        public async Task<bool> connect()
        {
            try
            {
                await this.client_sock.ConnectAsync(ip, port);
                recv();
                await handler.sendContinue(token);
                return true;
            }
            catch (Exception e)
            {
                await handler.sendErr(token, "Generic error -sockst connect- " + e.ToString());
                return false;
            }
        }

        public override void stop()
        {
            cts.Cancel();
            client_sock.Close();
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

        public async Task recv()
        {
            using (stream = this.client_sock.GetStream())
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
                        await handler.sendSocketData(token, res);
                    }
                    catch (Exception e)
                    {
                        stop();
                        await handler.sendErr(token, "Socket Recv error! " + e.ToString());
                        return;
                    }
                }
            }

        }
    }
}
