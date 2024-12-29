using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WSNet.SocketComm;
using WSNet.SSPIProxy;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using WSNetFramework.WSNet.Modules.SocketComm;
using System.IO;
using WSNetFramework.WSNet.Modules.Fileops;
using System.Diagnostics;

namespace WSNet
{
    class WSNetClinetHandler
    {
        Transport transport;
        Dictionary<string, SocketSession> socketlookup = new Dictionary<string, SocketSession>();
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
            await transport.Send(rawmessage);
        }

        public async Task sendOk(byte[] token)
        {
            CMDHeader okhdr = new CMDHeader(CMDType.OK, token, new byte[0]);
            await transport.Send(okhdr.to_bytes());
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
                                    bool res = await socket.connect();
                                    if (!res)
                                        socketlookup.Remove(tokenstr);
                                }
                                else
                                {
                                    SocketUDPClientSession socket = new SocketUDPClientSession(cmdhdr, cmd, this);
                                    socketlookup.Add(tokenstr, socket);
                                    bool res = await socket.connect();
                                    if (!res)
                                        socketlookup.Remove(tokenstr);
                                    //throw new Exception("UDP client not implemented!");
                                }
                            }
                            else
                            {
                                if(cmd.protocol == "TCP")
                                {
                                    if(cmd.bindtype == 1){
                                        //creating TCP server
                                        //SocketTCPServerSession socket = new SocketTCPServerSession(cmdhdr, cmd, this);
                                        //socketlookup.Add(tokenstr, socket);
                                        //bool res = await socket.bind();
                                        //if (!res)
                                        //    socketlookup.Remove(tokenstr);
                                        throw new Exception("TCP server not implemented!");

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
                    case CMDType.SDSRV:
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
                                // TODO: when servers are implemented, add the server object retrieval code part here!
                                // keep the code above intact because the same type of packet will be used by UDP clients as well!
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
                    case CMDType.RESOLV:
                        {
                            CMDResolv result = new CMDResolv();
                            CMDResolv packet = CMDResolv.parse(cmdhdr.data);
                            result.ip_or_hostname = await DNSResolver.ResolveAsync(packet.ip_or_hostname);
                            CMDHeader hdr = new CMDHeader(CMDType.RESOLV, cmdhdr.token, result.to_bytes());
                            await sendRaw(hdr.to_bytes());
                            break;
                        }
                    case CMDType.DIRRM:
                        {
                            CMDDirRM packet = CMDDirRM.parse(cmdhdr.data);
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
                            CMDDirMK packet = CMDDirMK.parse(cmdhdr.data);
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
                            CMDDirCopy packet = CMDDirCopy.parse(cmdhdr.data);
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
                            CMDDirMove packet = CMDDirMove.parse(cmdhdr.data);
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
                            CMDDirLS packet = CMDDirLS.parse(cmdhdr.data);
                            foreach (WSNFileEntry fe in Fileops.ListDirectoryContents(packet.path))
                            {
                                CMDHeader hdr = new CMDHeader(CMDType.FILEENTRY, cmdhdr.token, fe.to_bytes());
                                await sendRaw(hdr.to_bytes());
                            }
                            break;
                        }
                    case CMDType.FILECOPY:
                        {
                            try
                            {
                                CMDFileCopy packet = CMDFileCopy.parse(cmdhdr.data);
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
                                CMDFileMove packet = CMDFileMove.parse(cmdhdr.data);
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
                                CMDFileRM packet = CMDFileRM.parse(cmdhdr.data);
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
                                CMDFileOpen packet = CMDFileOpen.parse(cmdhdr.data);
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
                                CMDFileRead packet = CMDFileRead.parse(cmdhdr.data);
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
                                CMDFileData packet = CMDFileData.parse(cmdhdr.data);
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
                                CMDFileStat packet = CMDFileStat.parse(cmdhdr.data);
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
                await sendErr(cmdhdr.token, "Error in message processing! " + e.ToString());
                Console.WriteLine("Error in message processing! " + e.ToString());
            }
            
        }
    }
}
