using System;

#if WSSERVER
using WSNet.Servers.WebSocket;
#elif PIPESERVER
using WSNet.Servers.Pipe;
#endif

namespace WSNetFramework
{
    class Program
    {
        static void Main(string[] args)
        {
#if WSSERVER
            WebSocketsServer.StartMain(args);
#elif PIPESERVER
            PipeServer.StartMain(args);
#endif
        }
    }
}
