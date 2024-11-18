using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using WSNet.Servers;

namespace WSNetFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            MainServerWS(args);
            //MainSMBPipeServer(args);


        }

        static void MainServerWS(string[] args)
        {
            string ip = "0.0.0.0";
            int port = 8700;
            if (args.Length == 1)
            {
                if (args[0].ToLower() == "-h" || args[0].ToLower() == "--help" || args[0].ToLower() == "h" || args[0].ToLower() == "help")
                {
                    Console.WriteLine("WSNET websockets2TCP proxy.");
                    Console.WriteLine("Usage: proxy.exe <listen_ip> <listen_port> <PFX file> <PFX password>");
                    Console.WriteLine("PFX file and password only needed if you want a WS+SSL server");
                    Console.WriteLine("Default values: listen_ip=127.0.0.1 listen_port=8100 no SSL");
                    return;
                }
                ip = args[0];
            }
            if (args.Length >= 2)
            {
                ip = args[0];
                port = int.Parse(args[1]);
            }
            if (args.Length >= 4)
            {
                ip = args[0];
                port = int.Parse(args[1]);
            }
            RunWSServer(ip, port);
        }

        static void RunWSServer(string ip, int port, string pfx_file = "", string pfx_password = "")
        {
            WebSocketsServer wss = new WebSocketsServer(ip, port, pfx_file, pfx_password);
            var task = wss.Run();
            task.Wait();
        }

        static void MainSMBPipeServer(string[] args)
        {
            string pipename = "wsnet";
            if (args.Length == 1)
            {
                if (args[0].ToLower() == "-h" || args[0].ToLower() == "--help" || args[0].ToLower() == "h" || args[0].ToLower() == "help")
                {
                    Console.WriteLine("WSNET smbpipe server.");
                    Console.WriteLine("Usage: proxy.exe <listen_ip> <listen_port> <PFX file> <PFX password>");
                    Console.WriteLine("PFX file and password only needed if you want a WS+SSL server");
                    Console.WriteLine("Default values: listen_ip=127.0.0.1 listen_port=8100 no SSL");
                    return;
                }
                pipename = args[0];
            }
            RunSMBPipeServer(pipename);
        }

        static void RunSMBPipeServer(string pipename = "testpipe")
        {
            PipeServer ps = new PipeServer(pipename);
            var task = ps.Run();
            task.Wait();
        }
    }
}
