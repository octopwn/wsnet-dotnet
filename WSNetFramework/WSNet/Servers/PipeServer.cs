using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Security.AccessControl;
using System.IO;
using System.IO.Pipes;
using WSNet;
using System.Threading;

namespace WSNet.Servers
{
    class PipeTransport : Transport
    {
        NamedPipeServerStream pipe;
        SemaphoreSlim semaphoreSlim;
        
        public PipeTransport(NamedPipeServerStream pipe, SemaphoreSlim semaphoreSlim)
        {
            this.pipe = pipe;
            this.semaphoreSlim = semaphoreSlim;
        }

        public override async Task Send(byte[] data)
        {
            //await semaphoreSlim.WaitAsync();
            try
            {
                await pipe.WriteAsync(data, 0, data.Length);
                await pipe.FlushAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine("Send err" + e.ToString());
                //pipe.Close();
                throw e;
            }
            finally
            {
                //ch.Stop();
                

                //    semaphoreSlim.Release();
            }
        }
    }

    class PipeServer
    {
        private int numThreads = 100;
        private string pipeName;
        private SemaphoreSlim pipeAccessLock;
        int maxPacketSize = 200 * 1024;

        public PipeServer(string pipename, int numThreads = 100)
        {
            this.pipeName = pipename;
            this.numThreads = numThreads;
            this.pipeAccessLock = new SemaphoreSlim(1, numThreads);
        }

        static PipeSecurity CreateSystemIOPipeSecurity()
        {
            PipeSecurity security = new PipeSecurity();
            security.AddAccessRule(
                new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.CreateNewInstance | PipeAccessRights.ReadWrite, AccessControlType.Allow)
            );
            return security;
        }

        public async Task Run()
        {
            SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
            while (true)
            {
                await pipeAccessLock.WaitAsync();
                try
                {
                    PipeSecurity pipeSecurity = CreateSystemIOPipeSecurity();
                    NamedPipeServerStream pipeServer = new NamedPipeServerStream(pipeName,
                                               PipeDirection.InOut,
                                               -1, //numThreads,
                                               PipeTransmissionMode.Message,
                                               PipeOptions.Asynchronous,
                                               maxPacketSize,
                                               maxPacketSize,
                                               pipeSecurity,
                                               HandleInheritability.None);

                    await Task.Factory.FromAsync(pipeServer.BeginWaitForConnection, pipeServer.EndWaitForConnection, null);
                    handlePipeClient(pipeServer, semaphoreSlim);
                }
                catch(Exception e)
                {
                    throw e;
                }
                finally
                {
                    pipeAccessLock.Release();
                }
            }

            Console.WriteLine("\nServer threads exhausted, exiting.");
            Console.Read();
        }

        private async void handlePipeClient(NamedPipeServerStream pipe, SemaphoreSlim semaphoreSlim)
        {
            
            WSNetClinetHandler ch = new WSNetClinetHandler();
            PipeTransport pt = new PipeTransport(pipe, semaphoreSlim);
            ch.run(pt);
            try
            {
                while (pipe.IsConnected)
                {
                    //await semaphoreSlim.WaitAsync();
                    try
                    {
                        byte[] length_raw = new byte[4];
                        int read = await pipe.ReadAsync(length_raw, 0, length_raw.Length);
                        if (read == -1)
                        {
                            Console.WriteLine("Clinet disconnected!");
                            return;
                        }
                        if (read == 0)
                        {
                            Console.WriteLine("Length 0 read!");
                            continue;
                        }
                        int length = (int)ParseUtils.readUint32(length_raw, 0);
                        if (length > maxPacketSize)
                        {
                            throw new Exception("Packet too large!");
                        }
                        Console.WriteLine(length);
                        byte[] data = new byte[length-4];
                        read = await pipe.ReadAsync(data, 0, data.Length);
                        if (read == -1)
                        {
                            Console.WriteLine("Clinet disconnected!");
                            return;
                        }
                        byte[] packet = new byte[data.Length + 4];
                        Array.Copy(length_raw, packet, length_raw.Length);
                        Array.Copy(data, 0, packet, length_raw.Length, data.Length);
                        await ch.processMessage(packet);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("handlePipeClient err" + e.ToString());
                        throw e;
                    }
                    finally
                    {
                        //    semaphoreSlim.Release();
                        
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine("handlePipeClient ext err" + e.ToString());
            }
            finally
            {
                ch.Stop();
                //pipe.Close();
            }
            
        }
    }
}
