using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using WSNet.Protocol;

namespace WSNet.Modules.SocketComm
{

    class SocketUDPClientSession : SocketSession
    {
        string ip;
        int port;
        UdpClient client_sock;
        byte[] token;
        CancellationTokenSource cts = new CancellationTokenSource();
        ClinetHandler handler;

        public SocketUDPClientSession(CMDHeader cmdhdr, CMDConnect cmd, ClinetHandler handler)
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
                    client_sock = new UdpClient(ip, port);
                    _ = listenForPackets();
                    return true;
                }
                catch (Exception e)
                {
                    await handler.sendErr(token, "Generic error -sockst connect- " + e.ToString());
                    return false;
                }
            }
           

        private async Task listenForPackets()
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult packet = await client_sock.ReceiveAsync();
                    string endpointIP = packet.RemoteEndPoint.Address.ToString();
                    int endpointPort = packet.RemoteEndPoint.Port;
                    CMDSRVSD res = new CMDSRVSD(this.initiator_cmdhdr.token, endpointIP, endpointPort, packet.Buffer);
                    await handler.sendServerSocketData(token, res);
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
        }

        public override async Task<bool> send(CMDHeader cmdhdr)
        {
            try
            {
                CMDSRVSD cmd = (CMDSRVSD)cmdhdr.packet;
                await client_sock.SendAsync(cmd.data, cmd.data.Length, cmd.ip, cmd.port);
                return true;
            }
            catch (Exception e)
            {
                await handler.sendErr(token, "Socket send error " + e.ToString());
                return false;
            }
        }
    }
}
