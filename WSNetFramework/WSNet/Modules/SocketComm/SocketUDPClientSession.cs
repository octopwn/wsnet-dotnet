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

/*
namespace WSNetFramework.WSNet.SocketComm
{
    class SocketUDPClientSession : SocketSession
    {
        string ip;
        int port;
        UdpClient client_sock;
        byte[] token;
        AsyncQueue<byte[]> outQ;
        CancellationTokenSource cts = new CancellationTokenSource();
        bool EvtSocketClosed = false;

        public SocketUDPClientSession(CMDHeader cmdhdr, CMDConnect cmd, AsyncQueue<byte[]> outQ)
        {
            this.initiator_cmdhdr = cmdhdr;
            this.initiator_cmd = cmd;
            this.ip = cmd.ip;
            this.port = cmd.port;
            this.token = cmdhdr.token;
            this.outQ = outQ;
        }

        public async Task<Tuple<bool, byte[]>> connect()
        {
            try
            {
                client_sock = new UdpClient();
                client_sock.Connect(ip, port);
                recv();
                return new Tuple<bool, byte[]>(true, null);

            }
            catch (Exception e)
            {
                CMDErr err = new CMDErr(-1, "Generic error -sockst connect- " + e.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }
        }

        public override void stop()
        {
            cts.Cancel();
        }

        public override async Task<Tuple<bool, byte[]>> send(CMDHeader cmdhdr)
        {
            try
            {
                if (cts.IsCancellationRequested || EvtSocketClosed)
                {
                    throw new Exception("Socket already closed!");
                }
                await client_sock.SendAsync(cmdhdr.data, cmdhdr.data.Length);
                return new Tuple<bool, byte[]>(true, null);
            }
            catch (Exception e)
            {
                Console.WriteLine("Socket send Error! " + e.Message);
                EvtSocketClosed = true;
                cts.Cancel();

                CMDErr err = new CMDErr(-1, "send error " + e.ToString());
                return new Tuple<bool, byte[]>(false, err.to_bytes());
            }


        }

        public async Task recv()
        {
            while (!(cts.IsCancellationRequested || EvtSocketClosed))
            {
                try
                {
                    var receivedResults = await client_sock.ReceiveAsync(); //.WithCancellation(cts.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Socket recv Error! " + e.Message);
                    EvtSocketClosed = true;

                    CMDErr err = new CMDErr(-1, "recv error " + e.ToString());
                    CMDHeader reply = new CMDHeader(CMDType.ERR, token, err.to_bytes());
                    outQ.Enqueue(reply.to_bytes());
                    return;
                }
            }

        }
    }
}
*/