using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WSNet
{
    class ParseUtils
    {
        static public uint readUint32(byte[] data, int startpos = 0)
        {
            byte[] lenb = new byte[4];
            Array.Copy(data, startpos, lenb, 0, 4);
            Array.Reverse(lenb);
            return BitConverter.ToUInt32(lenb, 0);
        }

        static public uint readUshort(byte[] data, int startpos = 0)
        {
            byte[] lenb = new byte[2];
            Array.Copy(data, startpos, lenb, 0, 2);
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
        static public byte[] writeUint32(uint data)
        {
            byte[] res = BitConverter.GetBytes(data);
            Array.Reverse(res);
            return res;
        }
        static public byte[] writeUShort(ushort data)
        {
            byte[] res = BitConverter.GetBytes(data);
            Array.Reverse(res);
            return res;
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

        static public byte[] readBytes(byte[] data, int startpos = 0)
        {
            int slen = (int)ParseUtils.readUint32(data, startpos);
            if (slen == 0) return null;
            byte[] sdata = new byte[slen];
            Array.Copy(data, startpos + 4, sdata, 0, slen);
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
    }
}
