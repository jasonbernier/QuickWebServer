# Quick Web Server

Quick Web Server is a multi-threaded and lightweight web/file server built in C# that lets you upload and download files over HTTP/HTTPS. It serves files from a specified working directory and can be secured with HTTP Basic Authentication via the `--password` option. Additionally, the server logs events to a log file (`server.log`) and provides a simple monitoring endpoint. This can be used during penetration tests to quickly move files accross hosts.



## Features

- **Default Bindings:**  
  If no IP or port is specified, the server listens on all IP addresses (`+`) and port `8080`.

- **Default Working Directory:**  
  If no working directory is specified, the current working directory is used.

- **HTTPS Support:**  
  Use the `--https` flag to run the server over HTTPS (requires proper SSL certificate binding).

- **File Listing:**  
  Automatically displays all files in the working directory with clickable download links.

- **File Download:**  
  Download files by clicking the links.

- **File Upload:**  
  Upload files via a simple HTML form.

- **Multi-threaded:**  
  Handles multiple requests concurrently using asynchronous tasks.

- **Optional Authentication:**  
  Secure the server with HTTP Basic Authentication by specifying a password with the `--password` option.

- **Logging:**  
  Logs key events and errors to `server.log` and the console.

- **Monitoring:**  
  Access the `/monitor` endpoint to view basic metrics (total requests and error count) in JSON format.

- **Help Menu on Startup:**  
  The help menu is displayed every time the server is run.

- **--help Option:**  
  When specified, the help menu is displayed and the server exits immediately.
  
## Usage

### Command-Line Arguments

Run the executable with the following parameters:

QuickWebServer.exe [ip] [port] [workingDirectory] [--password <password>] [--https]

markdown
Copy

- **ip:** *(Optional)* IP address to bind to.  
  **Default:** `+` (listens on all IP addresses)

- **port:** *(Optional)* Port number to listen on.  
  **Default:** `8080`

- **workingDirectory:** *(Optional)* Directory path from which files are served and stored.  
  **Default:** Current working directory

- **--password <password>:** *(Optional)* Specifies the password for HTTP Basic Authentication.  
  If omitted, authentication is disabled.

- **--https:** *(Optional)* Flag to enable HTTPS.  
  **Note:** Ensure you have bound a valid SSL certificate to the port.

### Help Option

To display usage instructions and exit, run:

QuickWebServer.exe --help

markdown
Copy

### Examples

- **Using All Defaults (no authentication, HTTP):**

QuickWebServer.exe

markdown
Copy

- **Specifying IP, Port, and Working Directory (HTTP):**

QuickWebServer.exe 127.0.0.1 8081 "C:\MyFiles"

markdown
Copy

- **Specifying Authentication and HTTPS:**

QuickWebServer.exe 127.0.0.1 8080 "C:\MyFiles" --password mySecretPassword --https

markdown
Copy

### Monitoring

- Access the `/monitor` endpoint in your browser or via a tool like `curl` to see metrics:
http://<ip>:<port>/monitor

css
Copy
The endpoint returns a JSON response such as:
```json
{ "totalRequests": 123, "errorCount": 2 }
