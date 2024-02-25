using System;
using System.Text;
using System.Net;

namespace WSNet
{
    enum CMDType
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
    }
    class CMDHeader
    {
        public int total_length;
        public CMDType type;
        public byte[] token = new byte[16];
        public byte[] data;

        public CMDHeader()
        {

        }

        public CMDHeader(CMDType type, byte[] token, byte[] data)
        {
            this.token = token;
            this.data = data;
            this.type = type;

        }

        static public CMDHeader parse(byte[] data)
        {
            CMDHeader cmdhdr = new CMDHeader();
            cmdhdr.total_length = (int)ParseUtils.readUint32(data, 0);
            cmdhdr.type = (CMDType)ParseUtils.readUshort(data, 4);
            Array.Copy(data, 6, cmdhdr.token, 0, 16);
            cmdhdr.data = new byte[data.Length - 22];
            Array.Copy(data, 22, cmdhdr.data, 0, cmdhdr.data.Length);
            return cmdhdr;
        }

        public byte[] to_bytes()
        {

            total_length = data.Length + 22;
            byte[] blen = ParseUtils.writeUint32((uint)total_length);
            byte[] btype = ParseUtils.writeUShort((ushort)type);

            byte[][] rest = { blen, btype, token, data };
            return ParseUtils.Combine(rest);
        }
    }

    class CMDInfoReply
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

        public byte[] to_bytes()
        {
            byte[] bpid = ParseUtils.writeString(pid);
            byte[] busername = ParseUtils.writeString(username, "UTF16");
            byte[] bdomain = ParseUtils.writeString(domain, "UTF16");
            byte[] blogonserver = ParseUtils.writeString(logonserver, "UTF16");
            byte[] bcpuarch = ParseUtils.writeString(cpuarch);
            byte[] bhostname = ParseUtils.writeString(hostname, "UTF16");
            byte[] busersid = ParseUtils.writeString(usersid);


            byte[][] rest = { bpid, busername, bdomain, blogonserver, bcpuarch, bhostname, busersid };
            return ParseUtils.Combine(rest);
        }
    }


    class CMDErr
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(err.ToString());
            byte[] berrormsg = ParseUtils.writeString(errormsg);

            byte[][] rest = { berr, berrormsg };
            return ParseUtils.Combine(rest);
        }

    }


    class CMDConnect
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

        static public CMDConnect parse(byte[] data)
        {
            int ptr = 0;
            CMDConnect cmd = new CMDConnect();
            byte[] pb = new byte[3];
            Array.Copy(data, pb, 3);
            cmd.protocol = Encoding.ASCII.GetString(pb);
            if (data[3] == 0) cmd.bind = false;
            else cmd.bind = true;
            cmd.iptype = data[4];
            if (cmd.iptype == 4)
            {
                byte[] bip = new byte[4];
                Array.Copy(data, 5, bip, 0, 4);
                cmd.ip = new IPAddress(bip).ToString();
                cmd.port = (int)ParseUtils.readUshort(data, 9);
                ptr = 11;
            }
            else if (cmd.iptype == 6)
            {
                byte[] bip = new byte[16];
                Array.Copy(data, 5, bip, 0, 16);
                cmd.ip = new IPAddress(bip).ToString();
                cmd.port = (int)ParseUtils.readUshort(data, 22);
                ptr = 24;
            }
            else if (cmd.iptype == 0xff)
            {
                cmd.ip = ParseUtils.readString(data, 5);
                cmd.port = (int)ParseUtils.readUshort(data, 9 + cmd.ip.Length);
                ptr = 11 + cmd.ip.Length;
            }
            else throw new Exception("Unknown IP type " + cmd.iptype.ToString());
            cmd.bindtype = data[ptr];

            return cmd;
        }

    }

    class CMDAuthErr
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(err.ToString());
            byte[] berrormsg = ParseUtils.writeString(errormsg);

            byte[][] rest = { berr, berrormsg };
            return ParseUtils.Combine(rest);
        }

    }

    class CMDNTLMAuthReply
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(statusres.ToString());
            byte[] berrormsg = ParseUtils.writeString(ctxres.ToString());
            byte[] bauthres = ParseUtils.writeBytes(authres);

            byte[][] rest = { berr, berrormsg, bauthres };
            return ParseUtils.Combine(rest);
        }
    }

    class CMDNTLMChallengeReply
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(statusres.ToString());
            byte[] berrormsg = ParseUtils.writeString(ctxres.ToString());
            byte[] bauthres = ParseUtils.writeBytes(authres);

            byte[][] rest = { berr, berrormsg, bauthres };
            return ParseUtils.Combine(rest);
        }

    }

    class CMDKerberosReply
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(statusres.ToString());
            byte[] berrormsg = ParseUtils.writeString(ctxres.ToString());
            byte[] bauthres = ParseUtils.writeBytes(authres);

            byte[][] rest = { berr, berrormsg, bauthres };
            return ParseUtils.Combine(rest);
        }

    }

    class CMDSessionKeyReply
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(statusres.ToString());
            byte[] bauthres = ParseUtils.writeBytes(sessionkey);

            byte[][] rest = { berr, bauthres };
            return ParseUtils.Combine(rest);
        }

    }

    class CMDSequenceReply
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

        public byte[] to_bytes()
        {
            byte[] berr = ParseUtils.writeString(statusres.ToString());
            byte[] bauthres = ParseUtils.writeBytes(response);

            byte[][] rest = { berr, bauthres };
            return ParseUtils.Combine(rest);
        }

    }

    class CMDNTLMAuth
    {
        public string username;
        public int credusage;
        public int ctxattr;
        public string target;

        public CMDNTLMAuth() { }

        static public CMDNTLMAuth parse(byte[] data)
        {
            CMDNTLMAuth cmd = new CMDNTLMAuth();
            int ptr = 0;
            cmd.username = ParseUtils.readString(data, ptr);
            if (cmd.username == null) ptr += 4;
            else ptr += 4 + cmd.username.Length;
            cmd.credusage = int.Parse(ParseUtils.readString(data, ptr));
            ptr += 4 + cmd.credusage.ToString().Length;
            cmd.ctxattr = int.Parse(ParseUtils.readString(data, ptr));
            ptr += 4 + cmd.ctxattr.ToString().Length;
            cmd.target = ParseUtils.readString(data, ptr);
            return cmd;
        }
    }

    class CMDNTLMChall
    {
        public byte[] authdata;
        public int ctxattr;
        public string target;

        public CMDNTLMChall() { }

        static public CMDNTLMChall parse(byte[] data)
        {
            CMDNTLMChall cmd = new CMDNTLMChall();
            int ptr = 0;
            cmd.authdata = ParseUtils.readBytes(data, ptr);
            ptr = 4 + cmd.authdata.Length;
            cmd.ctxattr = int.Parse(ParseUtils.readString(data, ptr));
            ptr += 4 + cmd.ctxattr.ToString().Length;
            cmd.target = ParseUtils.readString(data, ptr);
            return cmd;
        }
    }

    class CMDKerberos
    {
        public string username;
        public int credusage;
        public int ctxattr;
        public string target;
        public byte[] authdata;

        public CMDKerberos() { }

        static public CMDKerberos parse(byte[] data)
        {
            CMDKerberos cmd = new CMDKerberos();
            int ptr = 0;
            cmd.username = ParseUtils.readString(data, ptr);
            if (cmd.username == null) ptr += 4;
            else ptr += 4 + cmd.username.Length;
            cmd.credusage = int.Parse(ParseUtils.readString(data, ptr));
            ptr += 4 + cmd.credusage.ToString().Length;
            cmd.ctxattr = int.Parse(ParseUtils.readString(data, ptr));
            ptr += 4 + cmd.ctxattr.ToString().Length;
            cmd.target = ParseUtils.readString(data, ptr);
            ptr += 4 + cmd.target.Length;
            cmd.authdata = ParseUtils.readBytes(data, ptr);
            return cmd;
        }
    }

    class CMDSRVSD
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
            catch (Exception e)
            {
                iptype = 0xff;
            }
        }

        public byte[] to_bytes()
        {
            byte[] bconnToken = ParseUtils.writeBytes(connectiontoken);
            byte[] biptype = ParseUtils.writeUShort(iptype);
            byte[] bip;
            try{
                IPAddress ip = IPAddress.Parse(this.ip);
                bip = ip.GetAddressBytes();
            }
            catch (Exception e){
                bip = ParseUtils.writeString(this.ip);
            }

            byte[] bport = ParseUtils.writeUShort((ushort)port);
            byte[][] rest = { bconnToken, biptype, bip, bport, data};
            return ParseUtils.Combine(rest);
        }

        static public CMDSRVSD parse(byte[] data)
        {
            int ptr = 0;
            CMDConnect cmd = new CMDConnect();
            byte[] pb = new byte[3];
            Array.Copy(data, pb, 3);
            cmd.protocol = Encoding.ASCII.GetString(pb);
            if (data[3] == 0) cmd.bind = false;
            else cmd.bind = true;
            cmd.iptype = data[4];
            if (cmd.iptype == 4)
            {
                byte[] bip = new byte[4];
                Array.Copy(data, 5, bip, 0, 4);
                cmd.ip = new IPAddress(bip).ToString();
                cmd.port = (int)ParseUtils.readUshort(data, 9);
                ptr = 11;
            }
            else if (cmd.iptype == 6)
            {
                byte[] bip = new byte[16];
                Array.Copy(data, 5, bip, 0, 16);
                cmd.ip = new IPAddress(bip).ToString();
                cmd.port = (int)ParseUtils.readUshort(data, 22);
                ptr = 24;
            }
            else if (cmd.iptype == 0xff)
            {
                cmd.ip = ParseUtils.readString(data, 5);
                cmd.port = (int)ParseUtils.readUshort(data, 9 + cmd.ip.Length);
                ptr = 11 + cmd.ip.Length;
            }
            else throw new Exception("Unknown IP type " + cmd.iptype.ToString());
            cmd.bindtype = data[ptr];

            return cmd;
        }

    }
}
