// modified code of
// MIT License - Copyright (c) 2016 Can Güney Aksakalli
// https://aksakalli.github.io/2014/02/24/simple-http-server-with-csparp.html

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace WSNetFramework.WSNet.Utils.ZipServer
{
    public class ZipServer : IDisposable
    {
        private object _LockObjectDispose = new object();
        private CancellationTokenSource _CancellationTokenSourceDisposed = new CancellationTokenSource();
        private readonly string[] _IndexFiles = {
            "index.html",
            "index.htm",
            "default.html",
            "default.htm"
        };

        private static IDictionary<string, string> _MimeTypeMappings = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
            #region extension to MIME type list
            {".asf", "video/x-ms-asf"},
            {".asx", "video/x-ms-asf"},
            {".avi", "video/x-msvideo"},
            {".bin", "application/octet-stream"},
            {".cco", "application/x-cocoa"},
            {".crt", "application/x-x509-ca-cert"},
            {".css", "text/css"},
            {".deb", "application/octet-stream"},
            {".der", "application/x-x509-ca-cert"},
            {".dll", "application/octet-stream"},
            {".dmg", "application/octet-stream"},
            {".ear", "application/java-archive"},
            {".eot", "application/octet-stream"},
            {".exe", "application/octet-stream"},
            {".flv", "video/x-flv"},
            {".gif", "image/gif"},
            {".hqx", "application/mac-binhex40"},
            {".htc", "text/x-component"},
            {".htm", "text/html"},
            {".html", "text/html"},
            {".ico", "image/x-icon"},
            {".img", "application/octet-stream"},
            {".iso", "application/octet-stream"},
            {".jar", "application/java-archive"},
            {".jardiff", "application/x-java-archive-diff"},
            {".jng", "image/x-jng"},
            {".jnlp", "application/x-java-jnlp-file"},
            {".jpeg", "image/jpeg"},
            {".jpg", "image/jpeg"},
            {".js", "application/x-javascript"},
            {".mml", "text/mathml"},
            {".mng", "video/x-mng"},
            {".mov", "video/quicktime"},
            {".mp3", "audio/mpeg"},
            {".mpeg", "video/mpeg"},
            {".mpg", "video/mpeg"},
            {".msi", "application/octet-stream"},
            {".msm", "application/octet-stream"},
            {".msp", "application/octet-stream"},
            {".pdb", "application/x-pilot"},
            {".pdf", "application/pdf"},
            {".pem", "application/x-x509-ca-cert"},
            {".pl", "application/x-perl"},
            {".pm", "application/x-perl"},
            {".png", "image/png"},
            {".prc", "application/x-pilot"},
            {".ra", "audio/x-realaudio"},
            {".rar", "application/x-rar-compressed"},
            {".rpm", "application/x-redhat-package-manager"},
            {".rss", "text/xml"},
            {".run", "application/x-makeself"},
            {".sea", "application/x-sea"},
            {".shtml", "text/html"},
            {".sit", "application/x-stuffit"},
            {".swf", "application/x-shockwave-flash"},
            {".tcl", "application/x-tcl"},
            {".tk", "application/x-tcl"},
            {".txt", "text/plain"},
            {".war", "application/java-archive"},
            {".wbmp", "image/vnd.wap.wbmp"},
            {".wmv", "video/x-ms-wmv"},
            {".xml", "text/xml"},
            {".xpi", "application/x-xpinstall"},
            {".zip", "application/zip"},
            {".json", "application/json"},
            {".wasm", "application/wasm"},
            {".svg", "image/svg+xml"},
            {".manifest", "text/cache-manifest"},
            #endregion
        };
        private Thread _ServerThread;
        private bool _AllowCors = true;
        private int _Port;
        private string _Address = "127.0.0.1";
        private string zipfilepath;
        private Action<Exception> _HandleException;

        public int Port { get { return _Port; } }

        public ZipServer(string zipfilepath, string address = "127.0.0.1", int port = 8000, Action<Exception> handleException = null)
        {
            this.zipfilepath = zipfilepath;
            _Address = address;
            _Port = port;
            _HandleException = handleException;
            _ServerThread = new Thread(Listen);
            _ServerThread.Start();
        }

        public void Dispose()
        {
            lock (_LockObjectDispose)
            {
                if (_CancellationTokenSourceDisposed.IsCancellationRequested) return;
                _CancellationTokenSourceDisposed.Cancel();
            }
        }

        private void Listen()
        {
            using (HttpListener httpListener = new HttpListener())
            {
                httpListener.Prefixes.Add($"http://{_Address}:{_Port.ToString()}/");
                httpListener.Start();
                using (CancellationTokenRegistration cancellationTokenRegistration = _CancellationTokenSourceDisposed.Token.Register(
                    httpListener.Abort))
                {
                    while (!_CancellationTokenSourceDisposed.IsCancellationRequested)
                    {
                        try
                        {
                            //Using this method because the GetContext method will not exit cleanly even when Stop or Abort are called.
                            Task<HttpListenerContext> task = httpListener.GetContextAsync();
                            task.Wait(_CancellationTokenSourceDisposed.Token);
                            Process(task.Result);
                        }
                        catch (Exception ex)
                        {
                            _HandleException?.Invoke(ex);
                        }
                    }
                }
            }
        }
        private int GetEmptyPort()
        {

            TcpListener tcpListener = new TcpListener(IPAddress.Loopback, this.Port);
            int port;
            tcpListener.Start();
            try
            {
                port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            }
            finally
            {
                tcpListener.Stop();
            }
            return port;
        }
        private void Process(HttpListenerContext httpListenerContext)
        {
            HttpListenerResponse httpListenerResponse = httpListenerContext.Response;
            string fileName = null;
            try
            {
                string filePath = GetRequestedFileName(httpListenerContext.Request);
                if (filePath == null || filePath == "" || filePath == "/")
                {
                    filePath = "index.html";
                }
                if (filePath.StartsWith("/"))
                {
                    filePath = filePath.Substring(1);
                }
                ReturnFile(filePath, httpListenerContext);
            }
            catch (Exception ex)
            {
                httpListenerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                string errorMessage = $"Error processing request";
                if (fileName != null) errorMessage += $" for file \"{fileName}\"";
                throw new Exception(errorMessage, ex);
            }
            finally
            {
                httpListenerResponse.OutputStream.Close();
            }
        }
        private void ReturnFile(string filePath, HttpListenerContext httpListenerContext)
        {
            try
            {
                using (ZipArchive archive = ZipFile.Open(zipfilepath, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = archive.GetEntry(filePath);
                    if (entry == null)
                    {
                        HttpListenerResponse httpListenerResponse = httpListenerContext.Response;
                        httpListenerResponse.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }
                    using (Stream input = entry.Open())
                    {
                        HttpListenerResponse httpListenerResponse = httpListenerContext.Response;
                        httpListenerResponse.ContentType = GetContentType(filePath);
                        httpListenerResponse.ContentLength64 = entry.Length;
                        httpListenerResponse.AddHeader("Date", DateTime.Now.ToString("r"));
                        httpListenerResponse.AddHeader("Last-Modified", entry.LastWriteTime.ToString("r"));
                        if (_AllowCors)
                            AddCorsHeaders(httpListenerResponse);
                        WriteInputStreamToResponse(input, httpListenerResponse.OutputStream);
                        httpListenerResponse.StatusCode = (int)HttpStatusCode.OK;
                        httpListenerResponse.OutputStream.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                HttpListenerResponse httpListenerResponse = httpListenerContext.Response;
                httpListenerResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }
        }
        private void AddCorsHeaders(HttpListenerResponse httpListenerResponse)
        {
            httpListenerResponse.AddHeader("Access-Control-Allow-Origin", "*");
        }
        private string GetRequestedFileName(HttpListenerRequest httpListenerRequest)
        {

            string fileName = httpListenerRequest.Url.AbsolutePath.Substring(1);
            return fileName;
        }
        private void WriteInputStreamToResponse(Stream inputStream, Stream outputStream)
        {

            byte[] buffer = new byte[1024 * 16];
            int nbytes;
            while ((nbytes = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                outputStream.Write(buffer, 0, nbytes);
        }
        private string GetContentType(string filePath)
        {
            string mime;
            if (_MimeTypeMappings.TryGetValue(Path.GetExtension(filePath), out mime))
                return mime;
            return "application/octet-stream";
        }
    }
}
