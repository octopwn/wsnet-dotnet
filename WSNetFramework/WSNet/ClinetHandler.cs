using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WSNet.Modules;
using WSNet.Modules.SocketComm;
using WSNet.Modules.Fileops;
using WSNet.Modules.SSPIProxy;
using WSNet.Protocol;

namespace WSNet
{
    class ClinetHandler
    {
        Transport transport;
        Dictionary<string, SocketSession> socketlookup = new Dictionary<string, SocketSession>();
        Dictionary<string, SocketServerSession> socketServerlookup = new Dictionary<string, SocketServerSession>();
        
        Dictionary<string, SSPISession> sspilookup = new Dictionary<string, SSPISession>();
        Dictionary<string, FileStream> filelookup = new Dictionary<string, FileStream>();
        Dictionary<string, string> filenamelookup = new Dictionary<string, string>();
        public CancellationTokenSource cts = new CancellationTokenSource();

        public async Task run(Transport transport)
        {
            this.transport = transport;
        }

        private async Task sendRaw(byte[] rawmessage)
        {
            WSNETDebug.LogPacket(rawmessage, "SEND");
            await transport.Send(rawmessage);
        }

        public async Task sendOk(byte[] token)
        {
            
            CMDHeader okhdr = new CMDHeader(CMDType.OK, token, new byte[0]);
            await sendRaw(okhdr.to_bytes());
        }

        public async Task sendContinue(byte[] token)
        {
            CMDHeader errhdr = new CMDHeader(CMDType.CONTINUE, token, new byte[0]);
            await sendRaw(errhdr.to_bytes());
        }
        public async Task sendErr(byte[] token, byte[] errdata)
        {
            CMDHeader errhdr = new CMDHeader(CMDType.ERR, token, errdata);
            await sendRaw(errhdr.to_bytes());
        }

        public async Task sendErr(byte[] token, string reason, int errcode = -1)
        {
            CMDErr err = new CMDErr(errcode, reason);
            await sendErr(token, err.to_bytes());
        }

        public async Task sendSocketData(byte[] token, byte[] data)
        {
            CMDHeader cmd = new CMDHeader(CMDType.SD, token, data);
            await sendRaw(cmd.to_bytes());
        }

        public async Task sendServerSocketData(byte[] token, CMDSRVSD res)
        {
            CMDHeader cmd = new CMDHeader(CMDType.SDSRV, token, res.to_bytes());
            await sendRaw(cmd.to_bytes());
        }

        public void Stop()
        {
            foreach(SocketSession sess in socketlookup.Values)
            {
                sess.stop();
            }
            foreach(SocketServerSession sess in socketServerlookup.Values)
            {
                sess.stop(); 
            }
        
            foreach(FileStream file in filelookup.Values)
            {
                file.Close();
            }
            foreach(SSPISession sspi in sspilookup.Values)
            {
                // todo: cleanup
            }
            filelookup = new Dictionary<string, FileStream>();
            sspilookup = new Dictionary<string, SSPISession>();
            socketlookup = new Dictionary<string, SocketSession>();
            socketServerlookup = new Dictionary<string, SocketServerSession>();
            filenamelookup = new Dictionary<string, string>();
            this.cts.Cancel();

        }
        async public Task processMessage(byte[] data)
        {
            CMDHeader cmdhdr = null;
            string tokenstr = null;
            try
            {

                cmdhdr = CMDHeader.parse(data);
                tokenstr = BitConverter.ToString(cmdhdr.token); //need to convert it to string because dictionary lookups. (using byte array will destroy the switch statement)
                WSNETDebug.Log("[IN][" + tokenstr + "]" + "[" + cmdhdr.type.ToString() + "]");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return;
            }
            
            try
            {
                switch (cmdhdr.type)
                {
                    case CMDType.CONNECT:
                        {
                            CMDConnect cmd = (CMDConnect)(cmdhdr.packet);
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
                                    SocketTCPClientSession clientSession = new SocketTCPClientSession(cmdhdr, cmd, this);
                                    bool res = await clientSession.connect();
                                    if (res)
                                        socketlookup.Add(tokenstr, clientSession);
                                }
                                else
                                {
                                    SocketUDPClientSession clientSession = new SocketUDPClientSession(cmdhdr, cmd, this);
                                    bool res = await clientSession.connect();
                                    if (res)
                                        socketlookup.Add(tokenstr, clientSession);
                                }
                            }
                            else
                            {
                                if(cmd.protocol == "TCP")
                                {
                                    if(cmd.bindtype == 1){
                                        //creating TCP server
                                        SocketTCPServerSession tcpServer = new SocketTCPServerSession(cmdhdr, cmd, this);
                                        bool res = await tcpServer.serve();
                                        if (res)
                                            socketServerlookup.Add(tokenstr, tcpServer);
                                    }
                                    else
                                    {
                                        throw new Exception("??? server not implemented!");
                                    }
                                }
                                else
                                {
                                        SocketUDPServerSession udpServer = new SocketUDPServerSession(cmdhdr, cmd, this);
                                        bool res = await udpServer.serve(cmd.bindtype);
                                        if (res)
                                            socketServerlookup.Add(tokenstr, udpServer);
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
                    case CMDType.SDSRV:
                        {
                            SocketServerSession socket;
                            if(socketServerlookup.TryGetValue(tokenstr, out socket))
                            {
                                bool res = await socket.send(cmdhdr);
                            }
                            else
                            {
                                await sendErr(cmdhdr.token, "No socket server session found for token");
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
                            CMDKerberos cmd = (CMDKerberos)cmdhdr.packet;
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
                            CMDNTLMAuth cmd = (CMDNTLMAuth)cmdhdr.packet;
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
                            CMDNTLMChall cmd = (CMDNTLMChall)cmdhdr.packet;
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
                    case CMDType.RESOLV:
                        {
                            CMDResolv result = new CMDResolv();
                            CMDResolv packet = (CMDResolv)cmdhdr.packet;
                            result.ip_or_hostname = await DNSResolver.ResolveAsync(packet.ip_or_hostname);
                            CMDHeader hdr = new CMDHeader(CMDType.RESOLV, cmdhdr.token, result.to_bytes());
                            await sendRaw(hdr.to_bytes());
                            break;
                        }
                    case CMDType.DIRRM:
                        {
                            CMDDirRM packet = (CMDDirRM)cmdhdr.packet;
                            try
                            {
                                if (Directory.Exists(packet.path))
                                {
                                    Directory.Delete(packet.path, true);
                                }
                                await sendOk(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.DIRMK:
                        {
                            CMDDirMK packet = (CMDDirMK)cmdhdr.packet;
                            try
                            {
                                if (!Directory.Exists(packet.path))
                                {
                                    // Create the directory
                                    Directory.CreateDirectory(packet.path);
                                }
                                await sendOk(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.DIRCOPY:
                        {
                            CMDDirCopy packet = (CMDDirCopy)cmdhdr.packet;
                            try
                            {
                                Fileops.CopyDirectory(packet.srcpath, packet.dstpath);
                                await sendOk(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.DIRMOVE:
                        {
                            CMDDirMove packet = (CMDDirMove)cmdhdr.packet;
                            try
                            {
                                Fileops.MoveDirectory(packet.srcpath, packet.dstpath);
                                await sendOk(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.DIRLS:
                        {
                            try
                            {
                                CMDDirLS packet = (CMDDirLS)cmdhdr.packet;
                                foreach (WSNFileEntry fe in Fileops.ListDirectoryContents(packet.path))
                                {
                                    CMDHeader hdr = new CMDHeader(CMDType.FILEENTRY, cmdhdr.token, fe.to_bytes());
                                    await sendRaw(hdr.to_bytes());
                                }
                                await sendOk(cmdhdr.token);
                                
                            }
                            catch(Exception e)
                            {
                                await sendErr(cmdhdr.token, e.ToString());
                            }
                            break;

                        }
                    case CMDType.FILECOPY:
                        {
                            try
                            {
                                CMDFileCopy packet = (CMDFileCopy)cmdhdr.packet;
                                File.Copy(packet.srcpath, packet.dstpath, true);
                                await sendOk(cmdhdr.token);
                                
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.FILEMOVE:
                        {
                            try
                            {
                                CMDFileMove packet = (CMDFileMove)cmdhdr.packet;
                                File.Move(packet.srcpath, packet.dstpath);
                                await sendOk(cmdhdr.token);

                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.FILERM:
                        {
                            try
                            {
                                CMDFileRM packet = (CMDFileRM)cmdhdr.packet;
                                File.Delete(packet.path);
                                await sendOk(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.FILEOPEN:
                        {
                            try
                            {
                                CMDFileOpen packet = (CMDFileOpen)cmdhdr.packet; 
                                FileMode fileMode = FileMode.Open;
                                FileStream temp = File.Open(packet.path, fileMode);
                                filelookup.Add(tokenstr, temp);
                                filenamelookup.Add(tokenstr, packet.path);
                                await sendContinue(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.FILEREAD:
                        {
                            try
                            {
                                CMDFileRead packet = (CMDFileRead)cmdhdr.packet;
                                FileStream temp;
                                if (!filelookup.TryGetValue(tokenstr, out temp))
                                {
                                    await sendErr(cmdhdr.token, "File not opened for token! ");
                                }
                                temp.Seek((long)packet.offset, SeekOrigin.Begin);
                                CMDFileData res = new CMDFileData();
                                res.data = new byte[packet.size];
                                temp.Read(res.data, 0, res.data.Length);
                                res.offset = packet.offset;
                                CMDHeader hdr = new CMDHeader(CMDType.FILEDATA, cmdhdr.token, res.to_bytes());
                                await sendRaw(hdr.to_bytes());
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.FILEDATA:
                        {
                            try
                            {
                                CMDFileData packet = (CMDFileData)cmdhdr.packet;
                                FileStream temp;
                                if (!filelookup.TryGetValue(tokenstr, out temp))
                                {
                                    await sendErr(cmdhdr.token, "File not opened for token! ");
                                }
                                temp.Seek((long)packet.offset, SeekOrigin.Begin);
                                temp.Write(packet.data, 0, packet.data.Length);
                                await sendContinue(cmdhdr.token);
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }
                            break;
                        }
                    case CMDType.FILESTAT:
                        {
                            try
                            {
                                CMDFileStat packet = (CMDFileStat)cmdhdr.packet;
                                string path;
                                if (!filenamelookup.TryGetValue(tokenstr, out path))
                                {
                                    await sendErr(cmdhdr.token, "File not opened for token! ");
                                }
                                WSNFileEntry fe = WSNFileEntry.fromFileInfo(new FileInfo(path));
                                CMDHeader hdr = new CMDHeader(CMDType.FILEENTRY, cmdhdr.token, fe.to_bytes());
                                await sendRaw(hdr.to_bytes());
                            }
                            catch (Exception e)
                            {
                                await sendErr(cmdhdr.token, "Error! " + e.ToString());
                            }

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
                Console.WriteLine("Error in message processing! " + e.ToString());
                await sendErr(cmdhdr.token, "Error in message processing! " + e.ToString());
                
            }
            
        }
    }
}
