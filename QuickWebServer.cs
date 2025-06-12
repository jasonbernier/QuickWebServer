// QuickWebServer.cs
//Written by Jason Bernier
//https://github.com/jasonbernier/QuickWebServer
// C# 7.3 compatible

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace QuickWebServer
{
    class Program
    {
        static string workingDirectory;
        static string authPassword = null;
        static int totalRequests = 0;
        static int errorCount = 0;

        static void Main(string[] args)
        {
            // Defaults
            string ip = "+";
            string port = "8080";
            workingDirectory = Directory.GetCurrentDirectory();
            bool useHttps = false;
            bool helpRequested = false;

            // Parse command-line options
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
                {
                    helpRequested = true;
                }
                else if (arg.Equals("--ip", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    ip = args[++i];
                }
                else if (arg.Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    port = args[++i];
                }
                else if (arg.Equals("--workingdir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    workingDirectory = args[++i];
                }
                else if (arg.Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    authPassword = args[++i];
                }
                else if (arg.Equals("--https", StringComparison.OrdinalIgnoreCase))
                {
                    useHttps = true;
                }
            }

            if (helpRequested)
            {
                PrintHelp();
                return;
            }
            PrintHelp();

            // Normalize 0.0.0.0 → +
            if (ip == "0.0.0.0")
            {
                Logger.Log("Note: HttpListener does not support 0.0.0.0 — using + instead");
                ip = "+";
            }

            // Ensure working directory exists or create it
            if (!Directory.Exists(workingDirectory))
            {
                try
                {
                    Directory.CreateDirectory(workingDirectory);
                    Logger.Log($"Working directory '{workingDirectory}' did not exist; created.");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error creating working directory '{workingDirectory}': {ex.Message}");
                    return;
                }
            }

            Logger.Log(authPassword != null ? "Authentication enabled." : "No authentication.");

            string scheme = useHttps ? "https" : "http";
            string prefix = string.Format("{0}://{1}:{2}/", scheme, ip, port);

            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                Logger.Log($"Quick Web Server started at {prefix}");
                Logger.Log($"Serving directory: {workingDirectory}");

                while (true)
                {
                    var ctx = listener.GetContext();
                    Task.Run(() => ProcessRequest(ctx));
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Listener error: {ex.Message}");
            }
            finally
            {
                listener.Close();
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine(
@"Quick Web Server (C# 7.3)

Usage: QuickWebServer.exe [--ip <address>] [--port <port>] [--workingdir <path>]
                        [--password <pw>] [--https] [--help]

Options:
  --ip <address>        Bind address (e.g. + for all or specific IP). Default: +
  --port <port>         Listen port. Default: 8080
  --workingdir <path>   Directory to serve. Default: current directory
  --password <pw>       Enable Basic Auth with this password
  --https               Serve over HTTPS (requires external cert binding)
  --help                Display this help and exit

Examples:
  QuickWebServer.exe
  QuickWebServer.exe --ip 0.0.0.0 --port 9000 --workingdir C:\Files --password secret --https
");
        }

        static void ProcessRequest(HttpListenerContext ctx)
        {
            totalRequests++;
            var req = ctx.Request;
            var resp = ctx.Response;
            string clientIp = req.RemoteEndPoint != null
                ? req.RemoteEndPoint.Address.ToString()
                : "unknown";

            // Log every incoming request with IP
            Logger.Log($"[{clientIp}] {req.HttpMethod} {req.Url}");

            try
            {
                // Monitoring endpoint
                if (req.Url.AbsolutePath.Equals("/monitor", StringComparison.OrdinalIgnoreCase))
                {
                    var json = string.Format("{{\"totalRequests\":{0},\"errorCount\":{1}}}",
                        totalRequests, errorCount);
                    resp.ContentType = "application/json";
                    Write(resp, json);
                    return;
                }

                // Authentication
                if (authPassword != null)
                {
                    var auth = req.Headers["Authorization"];
                    if (string.IsNullOrEmpty(auth) || !ValidateAuth(auth))
                    {
                        resp.StatusCode = 401;
                        resp.AddHeader("WWW-Authenticate", "Basic realm=\"QuickWebServer\"");
                        Write(resp, "Authentication required");
                        return;
                    }
                }

                // Routing
                if (req.HttpMethod == "GET")
                {
                    if (req.Url.AbsolutePath == "/")
                        ServeIndex(resp);
                    else if (req.Url.AbsolutePath.StartsWith("/download"))
                        ServeFile(ctx, req.QueryString["file"]);
                    else
                    {
                        resp.StatusCode = 404;
                        Write(resp, "Not found");
                    }
                }
                else if (req.HttpMethod == "POST" && req.Url.AbsolutePath == "/upload")
                {
                    HandleUpload(ctx);
                }
                else
                {
                    resp.StatusCode = 405;
                    Write(resp, "Method not allowed");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                Logger.Log($"[{clientIp}] Error: {ex.Message}");
            }
            finally
            {
                // Ensure the response is closed
                try
                {
                    resp.OutputStream.Close();
                }
                catch { }
            }
        }

        static bool ValidateAuth(string header)
        {
            if (!header.StartsWith("Basic ")) return false;
            try
            {
                var creds = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6)));
                var parts = creds.Split(':');
                return parts.Length == 2 && parts[1] == authPassword;
            }
            catch { return false; }
        }

        static void ServeIndex(HttpListenerResponse resp)
        {
            const string html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Quick Web Server</title>
  <style>
    body { font-family: Arial, sans-serif; margin: 0; padding: 0; }
    header { background-color: #004080; color: white; padding: 20px; text-align: center; }
    main { padding: 20px; }
    h2 { margin-top: 1em; }
    ul { list-style: none; padding: 0; }
    li { margin: 5px 0; }
    a { color: #0066cc; text-decoration: none; }
    a:hover { text-decoration: underline; }
    form { margin-top: 1em; }
  </style>
</head>
<body>
  <header>
    <h1>Quick Web Server</h1>
    <br>Written by Jason Bernier
  </header>
  <main>
    <h2>Available Files</h2>
    <ul>
      {{FILE_LIST}}
    </ul>
    <h2>Upload a File</h2>
    <form method=""post"" enctype=""multipart/form-data"" action=""/upload"">
      <input type=""file"" name=""file"" required />
      <button type=""submit"">Upload</button>
    </form>
  </main>
</body>
</html>";

            var listItems = new StringBuilder();
            foreach (var file in Directory.GetFiles(workingDirectory))
            {
                var name = Path.GetFileName(file);
                listItems.AppendFormat(
                    "<li><a href=\"/download?file={0}\">{1}</a></li>",
                    WebUtility.UrlEncode(name),
                    WebUtility.HtmlEncode(name)
                );
            }

            var page = html.Replace("{{FILE_LIST}}", listItems.ToString());
            resp.ContentType = "text/html; charset=UTF-8";
            var buf = Encoding.UTF8.GetBytes(page);
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
        }

        static void ServeFile(HttpListenerContext ctx, string fileName)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            string clientIp = req.RemoteEndPoint != null
                ? req.RemoteEndPoint.Address.ToString()
                : "unknown";

            if (string.IsNullOrEmpty(fileName))
            {
                resp.StatusCode = 400;
                Write(resp, "No file specified");
                return;
            }

            var path = Path.Combine(workingDirectory, Path.GetFileName(fileName));
            if (!File.Exists(path))
            {
                resp.StatusCode = 404;
                Write(resp, "File not found");
                return;
            }

            // Log download event
            Logger.Log($"[{clientIp}] Downloading file: {fileName}");

            resp.ContentType = "application/octet-stream";
            resp.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            resp.ContentLength64 = new FileInfo(path).Length;

            // Stream the file to avoid loading it fully into memory
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.CopyTo(resp.OutputStream);
            }
        }

        static void HandleUpload(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            string clientIp = req.RemoteEndPoint != null
                ? req.RemoteEndPoint.Address.ToString()
                : "unknown";

            if (!req.HasEntityBody || !req.ContentType.StartsWith("multipart/form-data"))
            {
                resp.StatusCode = 400;
                Write(resp, "Bad upload");
                return;
            }

            // Properly split boundary
            string[] boundaryParts = req.ContentType.Split(
                new[] { "boundary=" }, StringSplitOptions.None);
            var boundary = "--" + boundaryParts.Last();

            var ms = new MemoryStream();
            req.InputStream.CopyTo(ms);
            var bytes = ms.ToArray();
            var text = Encoding.UTF8.GetString(bytes);

            int idx = text.IndexOf("filename=\"");
            if (idx < 0)
            {
                resp.StatusCode = 400;
                Write(resp, "No file");
                return;
            }
            idx += 10;
            int end = text.IndexOf('"', idx);
            var name = text.Substring(idx, end - idx);

            int start = text.IndexOf("\r\n\r\n", end, StringComparison.Ordinal) + 4;
            int bidx = text.IndexOf(boundary, start, StringComparison.Ordinal);
            var fileData = bytes.Skip(start).Take(bidx - start).ToArray();

            var savePath = Path.Combine(workingDirectory, Path.GetFileName(name));
            File.WriteAllBytes(savePath, fileData);

            Logger.Log($"[{clientIp}] Uploaded file: {name}");

            resp.StatusCode = 303;
            resp.RedirectLocation = "/";
        }

        static void Write(HttpListenerResponse resp, string s)
        {
            var buf = Encoding.UTF8.GetBytes(s);
            resp.ContentLength64 = buf.Length;
            resp.OutputStream.Write(buf, 0, buf.Length);
        }
    }

    static class Logger
    {
        static readonly object _lock = new object();
        const string LogFile = "server.log";

        public static void Log(string msg)
        {
            lock (_lock)
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}";
                Console.WriteLine(line);
                try { File.AppendAllText(LogFile, line + Environment.NewLine); }
                catch { }
            }
        }
    }
}
