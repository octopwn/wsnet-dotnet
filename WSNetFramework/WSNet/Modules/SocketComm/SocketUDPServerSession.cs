using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using WSNet.Protocol;

namespace WSNet.Modules.SocketComm
{
    class SocketUDPServerSession : SocketServerSession
    {
        string ip;
        int port;
        UdpClient server_sock;
        byte[] token;
        CancellationTokenSource cts = new CancellationTokenSource();
        ClinetHandler handler;

        public SocketUDPServerSession(CMDHeader cmdhdr, CMDConnect cmd, ClinetHandler handler)
        {
            this.initiator_cmdhdr = cmdhdr;
            this.initiator_cmd = cmd;
            this.ip = cmd.ip;
            this.port = cmd.port;
            this.token = cmdhdr.token;
            this.handler = handler;
        }

        public async Task<bool> serve(int serverType)
        {
            try
            {
                // basic UDP server
                if (serverType == 1)
                {
                    server_sock = new UdpClient(ip, port);
                    _ = listenForPackets();
                    return true;
                }
                if (serverType == 2)
                {
                    IPEndPoint iPEndpoint = new IPEndPoint(IPAddress.Any, 5355);
                    server_sock = new UdpClient(iPEndpoint);
                    server_sock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    var multicastAddress = IPAddress.Parse("224.0.0.252");
                    server_sock.JoinMulticastGroup(multicastAddress);
                    _ = listenForPackets();
                    return true;
                }
                if (serverType == 3)
                {
                    IPEndPoint NbtBroadcastEndPoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 137); // Broadcast address
                    server_sock = new UdpClient(NbtBroadcastEndPoint);
                    _ = listenForPackets();
                    return true;
                }
                if (serverType == 4)
                {
                    IPEndPoint iPEndpoint = new IPEndPoint(IPAddress.Any, 5353);
                    server_sock = new UdpClient(iPEndpoint);
                    var multicastAddress = IPAddress.Parse("224.0.0.251");
                    server_sock.JoinMulticastGroup(multicastAddress);
                    _ = listenForPackets();
                    return true;

                }
                return false;
            }
            catch (Exception e)
            {
                WSNETDebug.Log("[UDPSERVER] Error creating listener! Reason: " + e.ToString());
                await handler.sendErr(token, "Generic error -UDP server creation- " + e.ToString());
                return false;
            }


        }

        private async Task listenForPackets()
        {
            await handler.sendContinue(token); //signaling the server that we have created the server

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult packet = await server_sock.ReceiveAsync();
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
            server_sock.Close();
        }



        public override async Task<bool> send(CMDHeader cmdhdr)
        {
            try
            {
                CMDSRVSD cmd = (CMDSRVSD)cmdhdr.packet;
                await server_sock.SendAsync(cmd.data, cmd.data.Length, cmd.ip, cmd.port);
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
