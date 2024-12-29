using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WSNet.SocketComm
{
    class SocketUDPClientSession : SocketSession
    {
        string ip;
        int port;
        byte[] token;
        UdpClient client_sock;
        WSNetClinetHandler handler;
        CancellationTokenSource cts = new CancellationTokenSource();

        public SocketUDPClientSession(CMDHeader cmdhdr, CMDConnect cmd, WSNetClinetHandler handler)
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
                UdpClient client_sock = new UdpClient(ip, port);
                _ = listen();
                return true;
            }
            catch (Exception e)
            {
                await handler.sendErr(token, "Generic error -sockst connect- " + e.ToString());
                return false;
            }
        }

        public async Task listen()
        {
            while (!cts.IsCancellationRequested)
            {
                UdpReceiveResult res = await client_sock.ReceiveAsync();
                IPEndPoint addr = res.RemoteEndPoint;
                CMDSRVSD cmd = new CMDSRVSD(token, addr.Address.ToString(), addr.Port, res.Buffer);
                await handler.sendServerSocketData(token, cmd);
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
                CMDSRVSD cmd = CMDSRVSD.parse(cmdhdr.data);
                await client_sock.SendAsync(cmd.data, cmd.data.Length, cmd.ip, cmd.port);
                return true;
            }
            catch(Exception e)
            {
                stop();
                return false;
            }
        }

    }
}
