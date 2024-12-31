using System;
<<<<<<< Updated upstream
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WSNet.SocketComm
=======
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using WSNet.Protocol;

namespace WSNet.Modules.SocketComm
>>>>>>> Stashed changes
{

    class SocketUDPClientSession : SocketSession
    {
        string ip;
        int port;
        byte[] token;
<<<<<<< Updated upstream
        UdpClient client_sock;
        WSNetClinetHandler handler;
        CancellationTokenSource cts = new CancellationTokenSource();

        public SocketUDPClientSession(CMDHeader cmdhdr, CMDConnect cmd, WSNetClinetHandler handler)
=======
        CancellationTokenSource cts = new CancellationTokenSource();
        ClinetHandler handler;

        public SocketUDPClientSession(CMDHeader cmdhdr, CMDConnect cmd, ClinetHandler handler)
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
            try
            {
                UdpClient client_sock = new UdpClient(ip, port);
                _ = listen();
                return true;
=======
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
>>>>>>> Stashed changes
            }
           

        private async Task listenForPackets()
        {
            while (!cts.IsCancellationRequested)
            {
<<<<<<< Updated upstream
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
=======
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
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
                CMDSRVSD cmd = CMDSRVSD.parse(cmdhdr.data);
=======
                CMDSRVSD cmd = (CMDSRVSD)cmdhdr.packet;
>>>>>>> Stashed changes
                await client_sock.SendAsync(cmd.data, cmd.data.Length, cmd.ip, cmd.port);
                return true;
            }
            catch(Exception e)
            {
<<<<<<< Updated upstream
                stop();
                return false;
            }
        }

=======
                await handler.sendErr(token, "Socket send error " + e.ToString());
                return false;
            }
        }
>>>>>>> Stashed changes
    }
}
