using System;
using System.Text;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WSNet.Protocol
{
    public enum CMDType
    {
        OK = 0,
        ERR = 1,
        NOP = 254,
        LOG = 2,
        STOP = 3,
        CONTINUE = 4,
        CONNECT = 5,
        DISCONNECT = 6,
        SD = 7,
        GETINFO = 8,
        GETINFOREPLY = 9,
        AUTHERR = 10,
        NTLMAUTH = 11,
        NTLMAUTHREPLY = 12,
        NTLMCHALL = 13,
        NTLMCHALLREPLY = 14,
        KERBEROS = 15,
        KERBEROSREPLY = 16,
        SESSIONKEY = 17,
        SESSIONKEYREPLY = 18,
        SEQUENCE = 19,
        SEQUENCEREPLY = 20,

        SDSRV = 200,
        RESOLV = 202,

        DIRLS = 300,
        DIRMK = 301,
        DIRRM = 302,
        DIRCOPY = 303,
        DIRMOVE = 304,
        FILEOPEN = 305,
        FILEREAD = 306,
        FILEDATA = 307,
        FILEENTRY = 308,
        FILECOPY = 309,
        FILEMOVE = 310,
        FILERM = 311,
        FILESTAT = 312,
    }

    public static class ParserRegistry
    {
        private static readonly IReadOnlyDictionary<CMDType, object> _noparse = new Dictionary<CMDType, object>
        {
            {CMDType.OK, null },
            {CMDType.ERR,null },
            {CMDType.NOP,null },
            {CMDType.CONTINUE,null },
            {CMDType.STOP,null },
            {CMDType.DISCONNECT, null },
            {CMDType.SD, null },
        };
        private static readonly IReadOnlyDictionary<CMDType, Type> _parsers = new Dictionary<CMDType, Type>
    {
        
        { CMDType.ERR, typeof(CMDErr) },
        { CMDType.CONNECT, typeof(CMDConnect) },
        { CMDType.NTLMAUTH, typeof(CMDNTLMAuth) },
        { CMDType.NTLMAUTHREPLY, typeof(CMDNTLMAuthReply) },
        { CMDType.NTLMCHALL, typeof(CMDNTLMChall) },
        { CMDType.NTLMCHALLREPLY, typeof(CMDNTLMChallengeReply) },
        { CMDType.KERBEROS, typeof(CMDKerberosReply) },
        { CMDType.KERBEROSREPLY, typeof(CMDKerberosReply) },
        { CMDType.SESSIONKEYREPLY, typeof(CMDSessionKeyReply) },
        { CMDType.SEQUENCEREPLY, typeof(CMDSequenceReply) },
        { CMDType.SDSRV, typeof(CMDSRVSD) },
        { CMDType.RESOLV, typeof(CMDResolv) },
        { CMDType.DIRLS, typeof(CMDDirLS) },
        { CMDType.DIRMK, typeof(CMDDirMK) },
        { CMDType.DIRRM, typeof(CMDDirRM) },
        { CMDType.DIRCOPY, typeof(CMDDirCopy) },
        { CMDType.DIRMOVE, typeof(CMDDirMove) },
        { CMDType.FILEOPEN, typeof(CMDFileOpen) },
        { CMDType.FILEREAD, typeof(CMDFileRead) },
        { CMDType.FILEDATA, typeof(CMDFileData) },
        { CMDType.FILEENTRY, typeof(WSNFileEntry) },
        { CMDType.FILECOPY, typeof(CMDFileCopy) },
        { CMDType.FILEMOVE, typeof(CMDFileMove) },
        { CMDType.FILERM, typeof(CMDFileRM) },
        { CMDType.FILESTAT, typeof(CMDFileStat) },
    };

        public static CMDBase CreateParserInstance(Type parserType)
        {
            if (Activator.CreateInstance(parserType) is CMDBase parser)
            {
                return parser;
            }
            throw new InvalidOperationException($"Type {parserType} is not a valid CMDBase.");
        }

        public static CMDBase GetParser(CMDType type)
        {
            if (_parsers.TryGetValue(type, out var parserType))
            {
                return CreateParserInstance(parserType);
            }
            throw new InvalidOperationException($"Parser for command type '{type}' ({(int)type}) is not registered.");
        }

        public static CMDBase ParsePacket(CMDType type, MemoryStream ms)
        {
            if (_noparse.ContainsKey(type))
                return null;
            if (!_parsers.ContainsKey(type))
            {
                throw new Exception($"CMD type {type.ToString()} has no parser definition!");
            }
            var parserType = _parsers[type];

            // Ensure the type is derived from CMDBase
            if (parserType.IsSubclassOf(typeof(CMDBase)) && parserType.GetMethod("parse", BindingFlags.Static | BindingFlags.Public) != null)
            {
                // Call the static Parse method on the parser type
                var parseMethod = parserType.GetMethod("parse");
                return (CMDBase)parseMethod.Invoke(null, new object[] { ms });
            }

            throw new InvalidOperationException($"No valid static Parse method found for type {parserType.Name}");
        }
    }


    public class CMDBase
    {
        public CMDBase() { }
        public CMDBase(byte[] data) { }
        virtual public byte[] to_bytes() { throw new NotImplementedException(); }
        static public CMDBase parse(MemoryStream ms) { throw new NotImplementedException(); }
    }

    class CMDHeader : CMDBase
        {
        public int total_length;
        public CMDType type;
        public byte[] token = new byte[16];
        public byte[] data;
        public CMDBase packet;

        public CMDHeader()
        {

        }

        public CMDHeader(CMDType type, byte[] token, byte[] data)
        {
            this.token = token;
            this.data = data;
            this.type = type;

        }

        public CMDHeader(MemoryStream ms, bool parsePacket = true)
        {
            total_length = (int)ParseUtils.readUint32(ms);
            type = (CMDType)ParseUtils.readUshort(ms);
            token = new byte[16];
            ms.Read(token, 0, 16);
            data = new byte[total_length - 22];
            ms.Read(data, 0, data.Length);
            ms.Seek(22, SeekOrigin.Begin);
            if (parsePacket)
                packet = ParserRegistry.ParsePacket(type, ms);
        }

        new static public CMDHeader parse(MemoryStream ms)
        {
            return new CMDHeader(ms);
        }

        static public CMDHeader parse(byte[] data)
        {
            using(MemoryStream ms = new MemoryStream(data))
            {
                return new CMDHeader(ms);
            }
        }

        static public CMDHeader parseHeader(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                return new CMDHeader(ms, false);
            }
        }        

        override public byte[] to_bytes()
        {
            total_length = data.Length + 22;
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeUint32(ms, (uint)total_length);
                ParseUtils.writeUShort(ms, (ushort)type);
                ms.Write(token, 0, token.Length);
                ms.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        public string pretty()
        {
            return $"[{BitConverter.ToString(token)}][{type.ToString()}]";
        }
    }

    class CMDInfoReply : CMDBase
    {
        //pid, username, domain, logonserver, cpuarch, hostname
        public string pid;
        public string username;
        public string domain;
        public string logonserver;
        public string cpuarch;
        public string hostname;
        public string usersid;
        public CMDInfoReply(string pid, string username, string domain, string logonserver, string cpuarch, string hostname, string usersid)
        {
            this.pid = pid;
            this.username = username;
            this.domain = domain;
            this.logonserver = logonserver;
            this.cpuarch = cpuarch;
            this.hostname = hostname;
            this.usersid = usersid;
        }

        override public byte[] to_bytes()
        {
            using(MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, pid);
                ParseUtils.writeString(ms, username, "UTF16");
                ParseUtils.writeString(ms, domain, "UTF16");
                ParseUtils.writeString(ms, logonserver, "UTF16");
                ParseUtils.writeString(ms, cpuarch);
                ParseUtils.writeString(ms, hostname, "UTF16");
                ParseUtils.writeString(ms, usersid);
                return ms.ToArray();
            }
        }

        new static public CMDInfoReply parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }
    }


    class CMDErr : CMDBase
        {
        int err;
        string errormsg;

        public CMDErr()
        {

        }
        public CMDErr(int err, string errormsg)
        {
            this.err = err;
            this.errormsg = errormsg;
        }

        override public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, err.ToString());
                ParseUtils.writeString(ms, errormsg);
                return ms.ToArray();
            }
        }

        new static public CMDErr parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }


    class CMDConnect : CMDBase
        {
        public string protocol;
        public bool bind;
        public int iptype;
        public string ip;
        public int port;
        public int bindtype;

        public CMDConnect()
        {

        }
        override public byte[] to_bytes()
        { 
            throw new NotImplementedException(); 
        }
       

        new static public CMDConnect parse(MemoryStream ms)
        {
            CMDConnect cmd = new CMDConnect();
            byte[] pb = new byte[3];
            ms.Read(pb, 0, 3);
            cmd.protocol = Encoding.ASCII.GetString(pb);
            cmd.bind = ParseUtils.readBool(ms);
            cmd.iptype = ms.ReadByte();
            if(cmd.iptype == 4)
            {
                byte[] bip = new byte[4];
                ms.Read(bip, 0, 4);
                cmd.ip = new IPAddress(bip).ToString();
                cmd.port = (int)ParseUtils.readUshort(ms);
            }
            else if (cmd.iptype == 6)
            {
                byte[] bip = new byte[16];
                ms.Read(bip, 0, 16);
                cmd.ip = new IPAddress(bip).ToString();
                cmd.port = (int)ParseUtils.readUshort(ms);
            }
            else if (cmd.iptype == 0xff)
            {
                cmd.ip = ParseUtils.readString(ms);
                cmd.port = (int)ParseUtils.readUshort(ms);
            }
            else throw new Exception("Unknown IP type " + cmd.iptype.ToString());
            cmd.bindtype = ms.ReadByte();
            WSNETDebug.Log(cmd.ToString());
            return cmd;
        }

        public override string ToString()
        {
            return $"[CMDConnect] protocol:{protocol} bind:{bind.ToString()} iptype: {iptype} ip: {ip} port:{port} bindtype:{bindtype}";
        }

    }

    class CMDAuthErr : CMDBase
    {
        int err;
        string errormsg;

        public CMDAuthErr()
        {

        }
        public CMDAuthErr(int err, string errormsg)
        {
            this.err = err;
            this.errormsg = errormsg;
        }

        override public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, err.ToString());
                ParseUtils.writeString(ms, errormsg);
                return ms.ToArray();
            }
        }

        new static public CMDAuthErr parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }

    class CMDNTLMAuthReply : CMDBase
    {
        int statusres;
        int ctxres;
        byte[] authres;

        public CMDNTLMAuthReply()
        {

        }
        public CMDNTLMAuthReply(int statusres, int ctxres, byte[] authres)
        {
            this.statusres = statusres;
            this.ctxres = ctxres;
            this.authres = authres;
        }

        new public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, statusres.ToString());
                ParseUtils.writeString(ms, ctxres.ToString());
                ParseUtils.writeBytes(ms, authres);

                return ms.ToArray();
            }
        }

        new static public CMDNTLMAuthReply parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }

    class CMDNTLMChallengeReply : CMDBase
    {
        int statusres;
        int ctxres;
        byte[] authres;

        public CMDNTLMChallengeReply()
        {

        }
        public CMDNTLMChallengeReply(int statusres, int ctxres, byte[] authres)
        {
            this.statusres = statusres;
            this.ctxres = ctxres;
            this.authres = authres;
        }

        new public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, statusres.ToString());
                ParseUtils.writeString(ms, ctxres.ToString());
                ParseUtils.writeBytes(ms, authres);
                return ms.ToArray();
            }
        }

        new static public CMDNTLMChallengeReply parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }

    class CMDKerberosReply : CMDBase
    {
        int statusres;
        int ctxres;
        byte[] authres;

        public CMDKerberosReply()
        {

        }
        public CMDKerberosReply(int statusres, int ctxres, byte[] authres)
        {
            this.statusres = statusres;
            this.ctxres = ctxres;
            this.authres = authres;
        }

        new public byte[] to_bytes()
        {
            using(MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, statusres.ToString());
                ParseUtils.writeString(ms, ctxres.ToString());
                ParseUtils.writeBytes(ms, authres);
                return ms.ToArray();

            }
        }

        new static public CMDKerberosReply parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }

    class CMDSessionKeyReply : CMDBase
    {
        int statusres;
        byte[] sessionkey;

        public CMDSessionKeyReply()
        {

        }
        public CMDSessionKeyReply(int statusres, byte[] sessionkey)
        {
            this.statusres = statusres;
            this.sessionkey = sessionkey;
        }

        new public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, statusres.ToString());
                ParseUtils.writeBytes(ms, sessionkey);
                return ms.ToArray();
            }
        }

        new static public CMDSessionKeyReply parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }

    class CMDSequenceReply : CMDBase
    {
        int statusres;
        byte[] response;

        public CMDSequenceReply()
        {

        }
        public CMDSequenceReply(int statusres, byte[] response)
        {
            this.statusres = statusres;
            this.response = response;
        }

        new public byte[] to_bytes()
        {
            using(MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, statusres.ToString());
                ParseUtils.writeBytes(response);
                return ms.ToArray();
            }
        }

        new static public CMDSequenceReply parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

    }

    class CMDNTLMAuth : CMDBase
    {
        public string username;
        public int credusage;
        public int ctxattr;
        public string target;

        public CMDNTLMAuth() { }



        new static public CMDNTLMAuth parse(MemoryStream ms)
        {
            CMDNTLMAuth cmd = new CMDNTLMAuth();
            cmd.username = ParseUtils.readString(ms);
            cmd.credusage = int.Parse(ParseUtils.readString(ms));
            cmd.ctxattr = int.Parse(ParseUtils.readString(ms));
            cmd.target = ParseUtils.readString(ms);
            return cmd;
        }
    }

    class CMDNTLMChall : CMDBase
    {
        public byte[] authdata;
        public int ctxattr;
        public string target;

        public CMDNTLMChall() { }

        new static public CMDNTLMChall parse(MemoryStream ms)
        {
            CMDNTLMChall cmd = new CMDNTLMChall();
            cmd.authdata = ParseUtils.readBytes(ms);
            cmd.ctxattr = int.Parse(ParseUtils.readString(ms));
            cmd.target = ParseUtils.readString(ms);
            return cmd;
        }
    }

    class CMDKerberos : CMDBase
    {
        public string username;
        public int credusage;
        public int ctxattr;
        public string target;
        public byte[] authdata;

        public CMDKerberos() { }


        new static public CMDKerberos parse(MemoryStream ms)
        {
            CMDKerberos cmd = new CMDKerberos();
            cmd.username = ParseUtils.readString(ms);
            cmd.credusage = int.Parse(ParseUtils.readString(ms));
            cmd.ctxattr = int.Parse(ParseUtils.readString(ms));
            cmd.target = ParseUtils.readString(ms);
            cmd.authdata = ParseUtils.readBytes(ms);
            return cmd;
        }
    }

    class CMDSRVSD : CMDBase
    {
        public byte[] connectiontoken;
        public byte[] data;
        public int iptype;
        public string ip;
        public int port;

        public CMDSRVSD()
        {

        }

        public CMDSRVSD(byte[] connectiontoken, string ip, int port, byte[] data)
        {
            this.connectiontoken = connectiontoken;
            this.data = data;
            this.ip = ip;
            this.port = port;
            try
            {
                IPAddress ipa = IPAddress.Parse(ip);
                if (ipa.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    iptype = 4;
                }
                else if (ipa.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    iptype = 6;
                }
                else
                {
                    iptype = 0xff;
                }
            }
            catch
            {
                iptype = 0xff;
            }
        }

        new public byte[] to_bytes()
        {
            using(MemoryStream ms = new MemoryStream())
            {
                ms.Write(connectiontoken, 0, connectiontoken.Length);
                ms.WriteByte((byte)iptype);
                byte[] bip;
                try
                {
                    IPAddress ip = IPAddress.Parse(this.ip);
                    bip = ip.GetAddressBytes();
                }
                catch
                {
                    bip = ParseUtils.writeString(this.ip);
                }

                ms.Write(bip, 0, bip.Length);
                ParseUtils.writeUShort(ms, (ushort)port);
                ms.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        new static public CMDSRVSD parse(MemoryStream ms)
        {
            CMDSRVSD cmd = new CMDSRVSD();
            cmd.connectiontoken = new byte[16];
            ms.Read(cmd.connectiontoken, 0, cmd.connectiontoken.Length);
            cmd.iptype = ms.ReadByte();
            if (cmd.iptype == 4)
            {
                byte[] bip = new byte[4];
                ms.Read(bip, 0, 4);
                cmd.ip = new IPAddress(bip).ToString();
            }
            else if (cmd.iptype == 6)
            {
                byte[] bip = new byte[16];
                ms.Read(bip, 0, 16);
                cmd.ip = new IPAddress(bip).ToString();
            }
            else if (cmd.iptype == 0xFF)
            {
                cmd.ip = ParseUtils.readString(ms);
            }
            else
            {
                throw new Exception("Unknown IP version! " + cmd.iptype.ToString());
            }
            cmd.port = (int)ParseUtils.readUshort(ms);
            cmd.data = new byte[(int)(ms.Length - ms.Position)];
            ms.Read(cmd.data, 0, cmd.data.Length);
            return cmd;
        }

    }

    class CMDResolv : CMDBase
    {
        public List<string> ip_or_hostname;

        public CMDResolv()
        {

        }

        new public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] count = ParseUtils.writeUint32((uint)ip_or_hostname.Count);
                ms.Write(count, 0, count.Length);
                ParseUtils.writeStringListToStream(ms, ip_or_hostname);
                return ms.ToArray();
            }
            
        }

        new static public CMDResolv parse(MemoryStream ms)
        {
            var res = new CMDResolv();
            res.ip_or_hostname = ParseUtils.readStringListFromStream(ms);
            return res;
        }

    }

    class CMDDirCopy : CMDBase
    {
        public string srcpath;
        public string dstpath;

        public CMDDirCopy()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }
        new static public CMDDirCopy parse(MemoryStream ms)
        {
            var res = new CMDDirCopy();
            res.srcpath = ParseUtils.readString(ms);
            res.dstpath = ParseUtils.readString(ms);
            return res;
        }

    }

    class CMDDirMove : CMDBase
    {
        public string srcpath;
        public string dstpath;

        public CMDDirMove()
        {

        }

       new  public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDDirMove parse(MemoryStream ms)
        {
            var res = new CMDDirMove();
            res.srcpath = ParseUtils.readString(ms);
            res.dstpath = ParseUtils.readString(ms);
            return res;
        }

    }

    class CMDDirLS : CMDBase
    {
        public string path;

        public CMDDirLS()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDDirLS parse(MemoryStream ms)
        {

            var res = new CMDDirLS();
            res.path = ParseUtils.readString(ms);
            return res;
        }

    }

    class CMDDirRM : CMDBase
    {
        public string path;

        public CMDDirRM()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDDirRM parse(MemoryStream ms)
        {
            var res = new CMDDirRM();
            res.path = ParseUtils.readString(ms);
            return res;
        }

    }

    class CMDDirMK : CMDBase
    {
        public string path;

        public CMDDirMK()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDDirMK parse(MemoryStream ms)
        {
            var res = new CMDDirMK();
            res.path = ParseUtils.readString(ms);
            return res;
        }
    }

    class CMDFileCopy : CMDBase
    {
        public string srcpath;
        public string dstpath;

        public CMDFileCopy()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDFileCopy parse(MemoryStream ms)
        {
            var res = new CMDFileCopy();
            res.srcpath = ParseUtils.readString(ms);
            res.dstpath = ParseUtils.readString(ms);
            return res;
        }

    }

    class CMDFileMove : CMDBase
    {
        public string srcpath;
        public string dstpath;

        public CMDFileMove()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDFileMove parse(MemoryStream ms)
        {
            var res = new CMDFileMove();
            res.srcpath = ParseUtils.readString(ms);
            res.dstpath = ParseUtils.readString(ms);
            return res;
        }
    }

    class CMDFileOpen : CMDBase
    {
        public string path;
        public string mode;

        public CMDFileOpen()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDFileOpen parse(MemoryStream ms)
        {
            var res = new CMDFileOpen();
            res.path = ParseUtils.readString(ms);
            res.mode = ParseUtils.readString(ms);
            return res;
        }
    }


    class CMDFileRead : CMDBase
    {
        public uint size;
        public UInt64 offset;

        public CMDFileRead()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDFileRead parse(MemoryStream ms)
        {
            var res = new CMDFileRead();
            res.size = ParseUtils.readUint32(ms);
            res.offset = ParseUtils.readUint64(ms);
            return res;
        }
    }

    class CMDFileRM : CMDBase
    {
        public string path;

        public CMDFileRM()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDFileRM parse(MemoryStream ms)
        {
            var res = new CMDFileRM();
            res.path = ParseUtils.readString(ms);
            return res;
        }
    }

    class CMDFileStat : CMDBase
    {

        public CMDFileStat()
        {

        }

        new public byte[] to_bytes()
        {
            throw new NotImplementedException();
        }

        new static public CMDFileStat parse(MemoryStream ms)
        {
            return new CMDFileStat(); // this is empty, only token is needed here
        }
    }

    class CMDFileData : CMDBase
    {
        public UInt64 offset;
        public byte[] data;

        public CMDFileData()
        {

        }

        new public byte[] to_bytes()
        {
            using(MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeUint64(ms, offset);
                ParseUtils.writeBytes(ms, data);
                return ms.ToArray();
            }
        }

        new static public CMDFileData parse(MemoryStream ms)
        {
            var res = new CMDFileData();
            res.offset = ParseUtils.readUint64(ms);
            res.data = ParseUtils.readBytes(ms);
            return res;
        }
    }

    class WSNFileEntry : CMDBase
    {
        public string root;
        public string name;
        public bool is_dir;
        public UInt64 size;
        public DateTime atime;
        public DateTime mtime;
        public DateTime ctime;

        public WSNFileEntry()
        {

        }

        new public byte[] to_bytes()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ParseUtils.writeString(ms, root);
                ParseUtils.writeString(ms, name);
                ParseUtils.writeBool(ms, is_dir);
                ParseUtils.writeUint64(ms, size);
                ParseUtils.writeUint64(ms, (ulong)atime.ToFileTimeUtc());
                ParseUtils.writeUint64(ms, (ulong)mtime.ToFileTimeUtc());
                ParseUtils.writeUint64(ms, (ulong)ctime.ToFileTimeUtc());
                return ms.ToArray();
            }
        }

        new static public WSNFileEntry parse(MemoryStream ms)
        {
            throw new NotImplementedException();
        }

        static public WSNFileEntry fromFileInfo(FileInfo file)
        {
            WSNFileEntry wSNFileEntry = new WSNFileEntry();
            wSNFileEntry.root = file.DirectoryName;
            wSNFileEntry.name = file.Name;
            wSNFileEntry.size = (ulong)file.Length;
            wSNFileEntry.ctime = file.CreationTime;
            wSNFileEntry.atime = file.LastAccessTime;
            wSNFileEntry.mtime = file.LastWriteTime;
            wSNFileEntry.is_dir = false;
            return wSNFileEntry;
        }
    }




}