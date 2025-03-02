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
        // The working directory from which files will be served and stored.
        static string workingDirectory;
        // Optional password for basic authentication. Set via the --password option.
        static string authPassword = null;

        // Monitoring counters.
        static int totalRequests = 0;
        static int errorCount = 0;

        // Main entry point of the application.
        static void Main(string[] args)
        {
            // Check if the user requested help.
            bool helpRequested = args.Any(a => a.Equals("--help", StringComparison.OrdinalIgnoreCase));
            if (helpRequested)
            {
                // Only display the help menu and then exit.
                PrintHelp();
                return;
            }
            else
            {
                // Always display the help menu on startup.
                PrintHelp();
            }

            // Default values.
            string ip = "+";
            string port = "8080";
            workingDirectory = Directory.GetCurrentDirectory();
            bool useHttps = false;

            // Prepare collections for positional arguments and options.
            List<string> positionalArgs = new List<string>();
            Dictionary<string, string> options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Parse arguments.
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg.StartsWith("--"))
                {
                    if (arg.Equals("--password", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        {
                            options["password"] = args[i + 1];
                            i++; // Skip next argument as it is the password.
                        }
                        else
                        {
                            Console.WriteLine("Error: --password option requires a value.");
                            return;
                        }
                    }
                    else if (arg.Equals("--https", StringComparison.OrdinalIgnoreCase))
                    {
                        options["https"] = "true";
                    }
                    // Other options can be added here.
                }
                else
                {
                    // This argument is positional.
                    positionalArgs.Add(arg);
                }
            }

            // Assign positional arguments if provided.
            if (positionalArgs.Count > 0)
                ip = positionalArgs[0];
            if (positionalArgs.Count > 1)
                port = positionalArgs[1];
            if (positionalArgs.Count > 2)
                workingDirectory = positionalArgs[2];

            // If the --password option was set, use its value.
            if (options.ContainsKey("password"))
                authPassword = options["password"];

            // If the --https option was set, enable HTTPS.
            if (options.ContainsKey("https"))
                useHttps = true;

            // Validate working directory.
            if (!Directory.Exists(workingDirectory))
            {
                Logger.Log("Error: The working directory does not exist.");
                return;
            }

            if (!string.IsNullOrEmpty(authPassword))
                Logger.Log("Authentication enabled.");
            else
                Logger.Log("No authentication enabled.");

            // Determine scheme based on the --https flag.
            string scheme = useHttps ? "https" : "http";

            // Build the listener prefix (e.g., http://+:8080/ or https://+:8080/).
            string prefix = $"{scheme}://{ip}:{port}/";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
                Logger.Log($"Quick Web Server started at {prefix}");
                Logger.Log($"Working directory: {workingDirectory}");

                // Continuously listen and process incoming requests.
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    Task.Run(() => ProcessRequest(context));
                }
            }
            catch (HttpListenerException hlex)
            {
                Logger.Log("HttpListenerException: " + hlex.Message);
            }
            catch (Exception ex)
            {
                Logger.Log("Exception: " + ex.Message);
            }
            finally
            {
                listener.Close();
            }
        }

        // Prints the help documentation to the console.
        static void PrintHelp()
        {
            Console.WriteLine("Quick Web Server");
            Console.WriteLine("Usage: QuickWebServer.exe [ip] [port] [workingDirectory] [--password <password>] [--https]");
            Console.WriteLine();
            Console.WriteLine("If omitted, defaults are:");
            Console.WriteLine("  ip: +            (listens on all IP addresses)");
            Console.WriteLine("  port: 8080");
            Console.WriteLine("  workingDirectory: current working directory");
            Console.WriteLine("  --password:      no authentication");
            Console.WriteLine("  --https:         use HTTPS (requires certificate binding)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --help          Display this help message.");
            Console.WriteLine();
        }

        // Processes an individual HTTP request.
        static void ProcessRequest(HttpListenerContext context)
        {
            totalRequests++;
            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                Logger.Log($"Request: {request.HttpMethod} {request.Url}");

                // Handle the monitoring endpoint.
                if (request.Url.AbsolutePath.Equals("/monitor", StringComparison.OrdinalIgnoreCase))
                {
                    ServeMonitor(response);
                    return;
                }

                // Check for authentication if enabled.
                if (!string.IsNullOrEmpty(authPassword))
                {
                    string authHeader = request.Headers["Authorization"];
                    if (string.IsNullOrEmpty(authHeader) || !ValidateAuth(authHeader))
                    {
                        response.StatusCode = 401;
                        response.AddHeader("WWW-Authenticate", "Basic realm=\"Quick Web Server\"");
                        WriteResponse(response, "Authentication required.");
                        return;
                    }
                }

                // Process GET requests.
                if (request.HttpMethod == "GET")
                {
                    if (request.Url.AbsolutePath == "/")
                    {
                        ServeIndex(response);
                    }
                    else if (request.Url.AbsolutePath.StartsWith("/download"))
                    {
                        string fileName = request.QueryString["file"];
                        ServeFile(response, fileName);
                    }
                    else
                    {
                        response.StatusCode = 404;
                        WriteResponse(response, "Not found");
                    }
                }
                // Process POST requests.
                else if (request.HttpMethod == "POST")
                {
                    if (request.Url.AbsolutePath == "/upload")
                    {
                        HandleUpload(context);
                    }
                    else
                    {
                        response.StatusCode = 404;
                        WriteResponse(response, "Not found");
                    }
                }
                else
                {
                    response.StatusCode = 405; // Method Not Allowed.
                    WriteResponse(response, "Method not allowed");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                Logger.Log("Error processing request: " + ex.Message);
            }
            finally
            {
                try { context.Response.OutputStream.Close(); } catch { }
            }
        }

        // Validates the HTTP Basic Authentication header.
        static bool ValidateAuth(string authHeader)
        {
            if (!authHeader.StartsWith("Basic "))
                return false;

            string encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            try
            {
                string decodedCredentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                // Expected format: "username:password" – we only check the password.
                int separatorIndex = decodedCredentials.IndexOf(':');
                if (separatorIndex < 0)
                    return false;
                string providedPassword = decodedCredentials.Substring(separatorIndex + 1);
                return providedPassword == authPassword;
            }
            catch
            {
                return false;
            }
        }

        // Serves the index page listing files and providing an upload form.
        static void ServeIndex(HttpListenerResponse response)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset=\"utf-8\" /></head><body>");
            sb.AppendLine("<h1>Quick Web Server - File List</h1>");
            sb.AppendLine("<ul>");

            // List each file in the working directory as a downloadable link.
            foreach (string file in Directory.GetFiles(workingDirectory))
            {
                string fileName = Path.GetFileName(file);
                sb.AppendLine($"<li><a href=\"/download?file={WebUtility.UrlEncode(fileName)}\">{fileName}</a></li>");
            }
            sb.AppendLine("</ul>");

            // Provide an HTML form for file uploads.
            sb.AppendLine("<h2>Upload File</h2>");
            sb.AppendLine("<form method=\"post\" enctype=\"multipart/form-data\" action=\"/upload\">");
            sb.AppendLine("<input type=\"file\" name=\"file\" />");
            sb.AppendLine("<input type=\"submit\" value=\"Upload\" />");
            sb.AppendLine("</form>");
            sb.AppendLine("</body></html>");

            byte[] buffer = Encoding.UTF8.GetBytes(sb.ToString());
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        // Serves a file download request.
        static void ServeFile(HttpListenerResponse response, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                response.StatusCode = 400;
                WriteResponse(response, "File name not specified.");
                return;
            }

            // Ensure that only files from the working directory are served.
            string filePath = Path.Combine(workingDirectory, Path.GetFileName(fileName));
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    response.ContentType = "application/octet-stream";
                    response.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    response.ContentLength64 = fileBytes.Length;
                    response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                }
                catch (Exception ex)
                {
                    response.StatusCode = 500;
                    WriteResponse(response, "Error reading file.");
                    Logger.Log("Error serving file: " + ex.Message);
                }
            }
            else
            {
                response.StatusCode = 404;
                WriteResponse(response, "File not found.");
            }
        }

        // Handles file upload requests.
        static void HandleUpload(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            if (!request.HasEntityBody)
            {
                response.StatusCode = 400;
                WriteResponse(response, "No data received.");
                return;
            }

            // Ensure the request's Content-Type is multipart/form-data.
            string contentType = request.ContentType;
            if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("multipart/form-data"))
            {
                response.StatusCode = 400;
                WriteResponse(response, "Invalid content type.");
                return;
            }

            // Extract the boundary from the Content-Type header.
            string boundary = "--" + contentType.Split(new string[] { "boundary=" }, StringSplitOptions.None)[1];
            byte[] boundaryBytes = Encoding.UTF8.GetBytes(boundary);

            // Read the entire request body.
            byte[] data;
            using (MemoryStream ms = new MemoryStream())
            {
                request.InputStream.CopyTo(ms);
                data = ms.ToArray();
            }

            // Convert the data to a string to parse multipart data.
            string dataStr = Encoding.UTF8.GetString(data);
            int headerIndex = dataStr.IndexOf("Content-Disposition");
            if (headerIndex == -1)
            {
                response.StatusCode = 400;
                WriteResponse(response, "Invalid multipart data.");
                return;
            }

            int filenameIndex = dataStr.IndexOf("filename=\"", headerIndex);
            if (filenameIndex == -1)
            {
                response.StatusCode = 400;
                WriteResponse(response, "No file uploaded.");
                return;
            }

            filenameIndex += "filename=\"".Length;
            int filenameEnd = dataStr.IndexOf("\"", filenameIndex);
            string fileName = dataStr.Substring(filenameIndex, filenameEnd - filenameIndex).Trim();
            if (string.IsNullOrEmpty(fileName))
            {
                response.StatusCode = 400;
                WriteResponse(response, "No file name provided.");
                return;
            }

            // Find the start of the file content.
            int dataStart = dataStr.IndexOf("\r\n\r\n", headerIndex);
            if (dataStart == -1)
            {
                response.StatusCode = 400;
                WriteResponse(response, "Invalid multipart data.");
                return;
            }
            dataStart += "\r\n\r\n".Length;

            // Find the ending boundary.
            int dataEnd = dataStr.IndexOf(boundary, dataStart);
            if (dataEnd == -1)
            {
                dataEnd = data.Length;
            }
            int fileDataLength = dataEnd - dataStart;

            // Build the file path.
            string filePath = Path.Combine(workingDirectory, Path.GetFileName(fileName));
            try
            {
                byte[] fileData = data.Skip(dataStart).Take(fileDataLength).ToArray();
                File.WriteAllBytes(filePath, fileData);

                // After successful upload, redirect back to the index page.
                response.StatusCode = 303; // See Other.
                response.RedirectLocation = "/";
            }
            catch (Exception ex)
            {
                response.StatusCode = 500;
                WriteResponse(response, "Error saving file.");
                Logger.Log("Upload error: " + ex.Message);
            }
        }

        // Serves the monitoring endpoint with basic metrics in JSON format.
        static void ServeMonitor(HttpListenerResponse response)
        {
            string json = $"{{ \"totalRequests\": {totalRequests}, \"errorCount\": {errorCount} }}";
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        // Writes a plain text response to the client.
        static void WriteResponse(HttpListenerResponse response, string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            response.ContentType = "text/plain";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }
    }

    // A simple thread-safe logger that writes to both the console and a log file.
    static class Logger
    {
        private static readonly object lockObj = new object();
        private static readonly string logFile = "server.log";

        public static void Log(string message)
        {
            lock (lockObj)
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                Console.WriteLine(line);
                try
                {
                    File.AppendAllText(logFile, line + Environment.NewLine);
                }
                catch
                {
                    // If logging to file fails, continue silently.
                }
            }
        }
    }
}
