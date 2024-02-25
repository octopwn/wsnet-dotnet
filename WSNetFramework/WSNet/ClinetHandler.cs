using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WSNet.SocketComm;
using WSNet.SSPIProxy;

namespace WSNet
{
    class WSNetClinetHandler
    {
        Transport transport;
        Dictionary<string, SocketSession> socketlookup = new Dictionary<string, SocketSession>();
        Dictionary<string, SSPISession> sspilookup = new Dictionary<string, SSPISession>();
        public CancellationTokenSource cts = new CancellationTokenSource();

        public async Task run(Transport transport)
        {
            this.transport = transport;
        }

        private async Task sendRaw(byte[] rawmessage)
        {
            await transport.Send(rawmessage);
        }

        public async Task sendOk()
        {

        }

        public async Task sendContinue(byte[] token)
        {
            CMDHeader errhdr = new CMDHeader(CMDType.CONTINUE, token, new byte[0]);
            await transport.Send(errhdr.to_bytes());
        }
        public async Task sendErr(byte[] token, byte[] errdata)
        {
            CMDHeader errhdr = new CMDHeader(CMDType.ERR, token, errdata);
            await transport.Send(errhdr.to_bytes());
        }

        public async Task sendErr(byte[] token, string reason, int errcode = -1)
        {
            CMDErr err = new CMDErr(errcode, reason);
            await sendErr(token, err.to_bytes());
        }

        public async Task sendSocketData(byte[] token, byte[] data)
        {
            CMDHeader cmd = new CMDHeader(CMDType.SD, token, data);
            await transport.Send(cmd.to_bytes());
        }

        public async Task sendServerSocketData(byte[] token, CMDSRVSD res)
        {
            CMDHeader cmd = new CMDHeader(CMDType.SDSRV, token, res.to_bytes());
            await transport.Send(cmd.to_bytes());
        }

        public void Stop()
        {
            foreach(SocketSession sess in socketlookup.Values)
            {
                sess.stop();
            }
            this.cts.Cancel();

        }
        async public Task processMessage(byte[] data)
        {
            CMDHeader cmdhdr = CMDHeader.parse(data);
            string tokenstr = BitConverter.ToString(cmdhdr.token); //need to convert it to string because dictionary lookups. (using byte array will destroy the switch statement)
            try
            {
                switch (cmdhdr.type)
                {
                    case CMDType.CONNECT:
                        {
                            CMDConnect cmd = CMDConnect.parse(cmdhdr.data);
                            if (socketlookup.ContainsKey(tokenstr))
                            {
                                throw new Exception("Token already exists!");
                            }

                            if (!cmd.bind)
                            {
                                // creating client socket
                                if(cmd.protocol == "TCP")
                                {
                                    //creating TCP client
                                    SocketTCPClientSession socket = new SocketTCPClientSession(cmdhdr, cmd, this);
                                    socketlookup.Add(tokenstr, socket);
                                    bool res = await socket.serve();
                                    if (!res)
                                        socketlookup.Remove(tokenstr);
                                }
                                else
                                {
                                    throw new Exception("UDP client not implemented!");
                                }
                            }
                            else
                            {
                                if(cmd.protocol == "TCP")
                                {
                                    if(cmd.bindtype == 1){
                                        //creating TCP server
                                        SocketTCPServerSession socket = new SocketTCPServerSession(cmdhdr, cmd, this);
                                        socketlookup.Add(tokenstr, socket);
                                        bool res = await socket.bind();
                                        if (!res)
                                            socketlookup.Remove(tokenstr);
                                    }
                                    else
                                    {
                                        throw new Exception("UDP server not implemented!");
                                    }
                                }
                                else
                                {
                                    throw new Exception("UDP server not implemented!");
                                }
                            }
                            break;
                        }
                    case CMDType.SD:
                        {
                            SocketSession socket;
                            if (socketlookup.TryGetValue(tokenstr, out socket))
                            {
                                bool res = await socket.send(cmdhdr);
                                if (!res)
                                    socketlookup.Remove(tokenstr);
                                
                            }
                            else
                            {
                                await sendErr(cmdhdr.token, "No socket session found for token");
                            }                           
                            break;
                        }
                    case CMDType.OK:
                    case CMDType.ERR:
                        {
                            SocketSession socket;
                            if (socketlookup.TryGetValue(tokenstr, out socket))
                            {
                                socket.stop();
                                socketlookup.Remove(tokenstr);
                            }
                            else
                            {
                                await sendErr(cmdhdr.token, "No socket session found for token");
                            }
                            break;
                        }
                        
                    case CMDType.KERBEROS:
                        {
                            CMDKerberos cmd = CMDKerberos.parse(cmdhdr.data);
                            SSPISession sspi;
                            if (!sspilookup.TryGetValue(tokenstr, out sspi))
                            {
                                sspi = new SSPISession();
                                sspilookup.Add(tokenstr, sspi);
                            }
                            Tuple<bool, byte[]> res = sspi.kerberos(cmd.username, cmd.credusage, cmd.target, cmd.ctxattr, cmd.authdata);
                            CMDHeader hdr;
                            if (res.Item1 == false) hdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, res.Item2);
                            else hdr = new CMDHeader(CMDType.KERBEROSREPLY, cmdhdr.token, res.Item2);
                            await sendRaw(hdr.to_bytes());

                            break;
                        }
                    case CMDType.NTLMAUTH:
                        {
                            CMDNTLMAuth cmd = CMDNTLMAuth.parse(cmdhdr.data);
                            SSPISession sspi;
                            if (!sspilookup.TryGetValue(tokenstr, out sspi))
                            {
                                sspi = new SSPISession();
                                sspilookup.Add(tokenstr, sspi);
                            }

                            Tuple<bool, byte[]> res = sspi.ntlmauth(cmd.username, cmd.credusage, cmd.target, cmd.ctxattr);
                            CMDHeader hdr;
                            if (res.Item1 == false) hdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, res.Item2);
                            else hdr = new CMDHeader(CMDType.NTLMAUTHREPLY, cmdhdr.token, res.Item2);
                            await sendRaw(hdr.to_bytes());

                            break;
                        }
                    case CMDType.NTLMCHALL:
                        {
                            CMDNTLMChall cmd = CMDNTLMChall.parse(cmdhdr.data);
                            SSPISession sspi;
                            if (!sspilookup.TryGetValue(tokenstr, out sspi))
                            {
                                CMDAuthErr err = new CMDAuthErr(-1, "No session found for token");
                                byte[] erres = err.to_bytes();
                                CMDHeader errhdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, erres);
                                await sendRaw(errhdr.to_bytes());
                            }
                            Tuple<bool, byte[]> res = sspi.ntlmchallenge(cmd.ctxattr, cmd.authdata, cmd.target);
                            CMDHeader hdr;
                            if (res.Item1 == false) hdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, res.Item2);
                            else hdr = new CMDHeader(CMDType.NTLMCHALLREPLY, cmdhdr.token, res.Item2);
                            await sendRaw(hdr.to_bytes());

                            break;
                        }
                    case CMDType.SEQUENCE:
                        {
                            SSPISession sspi;
                            if (!sspilookup.TryGetValue(tokenstr, out sspi))
                            {
                                CMDAuthErr err = new CMDAuthErr(-1, "No session found for token");
                                byte[] erres = err.to_bytes();
                                CMDHeader errhdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, erres);
                                await sendRaw(errhdr.to_bytes());
                            }
                            Tuple<bool, byte[]> res = sspi.sequenceno();
                            CMDHeader hdr;
                            if (res.Item1 == false) hdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, res.Item2);
                            else hdr = new CMDHeader(CMDType.SEQUENCEREPLY, cmdhdr.token, res.Item2);
                            await sendRaw(hdr.to_bytes());

                            break;

                        }
                    case CMDType.SESSIONKEY:
                        {
                            SSPISession sspi;
                            if (!sspilookup.TryGetValue(tokenstr, out sspi))
                            {
                                CMDAuthErr err = new CMDAuthErr(-1, "No session found for token");
                                byte[] erres = err.to_bytes();
                                CMDHeader errhdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, erres);
                                await sendRaw(errhdr.to_bytes());
                            }
                            Tuple<bool, byte[]> res = sspi.sessionkey();
                            CMDHeader hdr;
                            if (res.Item1 == false) hdr = new CMDHeader(CMDType.AUTHERR, cmdhdr.token, res.Item2);
                            else hdr = new CMDHeader(CMDType.SESSIONKEYREPLY, cmdhdr.token, res.Item2);
                            await sendRaw(hdr.to_bytes());

                            break;
                        }
                    case CMDType.GETINFO:
                        {
                            byte[] res = BasicInfo.getinfo();
                            CMDHeader hdr = new CMDHeader(CMDType.GETINFOREPLY, cmdhdr.token, res);
                            await sendRaw(hdr.to_bytes());
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Unexpected command recieved! Type: " + cmdhdr.type.ToString());
                            break;
                        }
                }
            }
            catch(Exception e)
            {
                await sendErr(cmdhdr.token, "Error in message processing! " + e.ToString());
                Console.WriteLine("Error in message processing! " + e.ToString());
            }
            
        }
    }
}
