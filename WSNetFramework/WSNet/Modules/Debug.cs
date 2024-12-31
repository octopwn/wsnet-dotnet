using System;
using System.Diagnostics;
using WSNet.Protocol;

namespace WSNet
{
    internal class WSNETDebug
    {
        [Conditional("DEBUG")]
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }

        [Conditional("DEBUG")]
        public static void LogPacket(byte[] packetData, string pre="")
        {
            CMDHeader cmdhdr = CMDHeader.parseHeader(packetData);
            Log($"[{pre}]" + cmdhdr.pretty());
        }

        [Conditional("DEBUG")]
        public static void LogPacketDeep(byte[] packetData, string pre = "")
        {
            CMDHeader cmdhdr = CMDHeader.parse(packetData);
            Log($"[{pre}]" + cmdhdr.pretty());
        }


    }
}
