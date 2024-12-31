using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace WSNet.Protocol
{
    class ParseUtils
    {
        static public bool readBool(MemoryStream ms)
        {
            return Convert.ToBoolean(ms.ReadByte());
        }
        static public uint readUint32(byte[] data, int startpos = 0)
        {
            byte[] lenb = new byte[4];
            Array.Copy(data, startpos, lenb, 0, 4);
            Array.Reverse(lenb);
            return BitConverter.ToUInt32(lenb, 0);
        }

        static public uint readUint32(MemoryStream ms)
        {
            byte[] lenb = new byte[4];
            ms.Read(lenb, 0, lenb.Length);
            Array.Reverse(lenb);
            return BitConverter.ToUInt32(lenb, 0);
        }

        static public UInt64 readUint64(MemoryStream ms)
        {
            byte[] lenb = new byte[8];
            ms.Read(lenb, 0, lenb.Length);
            Array.Reverse(lenb);
            return BitConverter.ToUInt64(lenb, 0);
        }

        static public uint readUshort(byte[] data, int startpos = 0)
        {
            byte[] lenb = new byte[2];
            Array.Copy(data, startpos, lenb, 0, 2);
            Array.Reverse(lenb);
            return BitConverter.ToUInt16(lenb, 0);
        }

        static public uint readUshort(MemoryStream ms)
        {
            byte[] lenb = new byte[2];
            ms.Read(lenb, 0, lenb.Length);
            Array.Reverse(lenb);
            return BitConverter.ToUInt16(lenb, 0);
        }


        static public string readString(byte[] data, int startpos = 0)
        {
            int slen = (int)ParseUtils.readUint32(data, startpos);
            if (slen == 0) return null;
            byte[] sdata = new byte[slen];
            Array.Copy(data, startpos + 4, sdata, 0, slen);
            string res = Encoding.ASCII.GetString(sdata);
            return res;
        }

        static public string readString(MemoryStream ms)
        {
            uint slen = ParseUtils.readUint32(ms);
            byte[] sdata = new byte[slen];
            ms.Read(sdata, 0, sdata.Length);
            return Encoding.ASCII.GetString(sdata);
        }

        static public List<string> readStringListFromStream(MemoryStream ms)
        {
            List<string> res = new List<string>();
            uint count = ParseUtils.readUint32(ms);
            for (int i = 0; i < count; i++)
            {
                res.Add(ParseUtils.readString(ms));
            }
            return res;
        }


        static public (int, string) readStringWithLen(byte[] data, int startpos = 0)
        {
            int slen = (int)ParseUtils.readUint32(data, startpos);
            if (slen == 0) return (0, "");
            byte[] sdata = new byte[slen];
            Array.Copy(data, startpos + 4, sdata, 0, slen);
            string res = Encoding.ASCII.GetString(sdata);
            return (slen, res );
        }

        static public void writeStringListToStream(MemoryStream ms, List<string> texts)
        {
            byte[] lendata = ParseUtils.writeUint32((uint)texts.Count);
            ms.Write(lendata, 0, lendata.Length);
            foreach(string text in texts)
            {
                byte[] sdata = ParseUtils.writeString(text);
                ms.Write(sdata, 0, sdata.Length);
            }

        }

        static public byte[] writeUint64(UInt64 data)
        {
            byte[] res = BitConverter.GetBytes(data);
            Array.Reverse(res);
            return res;
        }

        static public void writeUint64(MemoryStream ms, UInt64 data)
        {
            byte[] res = BitConverter.GetBytes(data);
            Array.Reverse(res);
            ms.Write(res, 0, res.Length);
        }


        static public void writeBool(MemoryStream ms, bool data)
        {
            ms.WriteByte(Convert.ToByte(data));
        }

        static public byte[] writeUint32(uint data)
        {
            byte[] res = BitConverter.GetBytes(data);
            Array.Reverse(res);
            return res;
        }

        static public void writeUint32(MemoryStream ms, uint data)
        {
            ms.Write(ParseUtils.writeUint32(data), 0, 4);
        }

        static public byte[] writeUShort(ushort data)
        {
            byte[] res = BitConverter.GetBytes(data);
            Array.Reverse(res);
            return res;
        }

        static public void writeUShort(MemoryStream ms, ushort data)
        {
            ms.Write(ParseUtils.writeUShort(data), 0, 2);
        }


        static public byte[] writeString(string data, string encoding = "ASCII")
        {
            byte[] temp;
            if (encoding == "ASCII") temp = Encoding.ASCII.GetBytes(data);
            else if (encoding == "UTF16") temp = Encoding.Unicode.GetBytes(data);
            else throw new Exception("Unknown encoding! " + encoding);
            byte[] res = new byte[4 + temp.Length];
            Array.Copy(writeUint32((uint)temp.Length), res, 4);
            Array.Copy(temp, 0, res, 4, temp.Length);
            return res;
        }

        static public void writeString(MemoryStream ms, string data, string encoding = "ASCII")
        {
            byte[] temp = writeString(data, encoding);
            ms.Write(temp, 0, temp.Length);
        }

        static public byte[] writeBytes(byte[] data)
        {
            byte[] temp;
            if (data != null && data.Length > 0)
            {
                temp = new byte[4 + data.Length];
                Array.Copy(writeUint32((uint)data.Length), temp, 4);
                Array.Copy(data, 0, temp, 4, data.Length);
                return temp;
            }

            temp = writeUint32(4);
            return temp;
        }

        static public void writeBytes(MemoryStream ms, byte[] data)
        {
            byte[] temp = ParseUtils.writeBytes(data);
            ms.Write(temp, 0, temp.Length);
        }

        static public byte[] readBytes(byte[] data, int startpos = 0)
        {
            int slen = (int)ParseUtils.readUint32(data, startpos);
            if (slen == 0) return null;
            byte[] sdata = new byte[slen];
            Array.Copy(data, startpos + 4, sdata, 0, slen);
            return sdata;
        }

        static public byte[] readBytes(MemoryStream ms)
        {
            int slen = (int)ParseUtils.readUint32(ms);
            byte[] sdata = new byte[slen];
            ms.Read(sdata, 0, slen);
            return sdata;
        }

        static public byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }

        public static bool IsIpAddress(string input)
        {
            return IPAddress.TryParse(input, out _);
        }
    }
}
