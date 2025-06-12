# Quick Web Server
Written by Jason Bernier

A simple, multi‐threaded HTTP/HTTPS file server written in C# 7.3.  
Provides file listing, upload, and streamed download with Basic Authentication, IP‐based logging, and a built‐in monitoring endpoint.

---

## Features

- **HTML5 UI** with a “Quick Web Server” banner  
- **File Listing**: Browse and download files in a directory  
- **File Upload**: Secure multipart‐form uploads  
- **Streamed Downloads**: No browser hangs on large files  
- **Basic Authentication**: Optional `--password` protection  
- **IP Logging**: Logs client IP for all requests, uploads, and downloads  
- **Monitoring Endpoint**: `/monitor` → JSON metrics  
- **Command-line Configuration**:
  - `--ip <address>` (e.g. `+` or `0.0.0.0`)  
  - `--port <port>`  
  - `--workingdir <path>`  
  - `--password <pw>`  
  - `--https`  
  - `--help`  
- **Auto-create Working Directory** if it doesn’t exist  
- **Console & File Logging**: `server.log`  

---

## Requirements

- [.NET SDK (2.1+) or newer](https://dotnet.microsoft.com/download)  
- Windows, Linux, or macOS  
- If using `--https`, you must bind an SSL certificate manually (e.g. via `netsh` on Windows).

---

## Building

1. Clone your repository:
   ```bash
   git clone https://github.com/yourusername/QuickWebServer.git
   cd QuickWebServer
   ```
2. Build with the .NET CLI:
   ```bash
   dotnet build -c Release
   ```
3. The compiled executable will be in:
   ```
   bin/Release/netcoreapp2.1/QuickWebServer.exe
   ```
   (or similar, depending on your target framework).

---

## Usage

```bash
QuickWebServer.exe [--ip <address>] [--port <port>] [--workingdir <path>]
                  [--password <pw>] [--https] [--help]
```

- `--ip <address>`  
  Bind to a specific IP (e.g. `127.0.0.1`), or `+` / `0.0.0.0` for all interfaces. Default: `+`

- `--port <port>`  
  Port to listen on. Default: `8080`

- `--workingdir <path>`  
  Directory to serve files from. Default: current working directory

- `--password <pw>`  
  Enable Basic Authentication with this password. If omitted, no auth is required.

- `--https`  
  Serve over HTTPS. Requires you to have bound an SSL certificate externally.

- `--help`  
  Display usage instructions and exit.

### Examples

- **Default (HTTP, port 8080, current directory)**  
  ```bash
  QuickWebServer.exe
  ```

- **Custom IP, port and directory**  
  ```bash
  QuickWebServer.exe --ip 0.0.0.0 --port 9000 --workingdir D:\Shared
  ```

- **Enable password & HTTPS**  
  ```bash
  QuickWebServer.exe --workingdir /var/www/files --password MySecret123 --https
  ```

- **Show help**  
  ```bash
  QuickWebServer.exe --help
  ```

---

## Endpoints

- **GET /**  
  HTML page with banner, file list, and upload form.

- **GET /download?file=NAME**  
  Streams the specified file as an attachment. Logs client IP and filename.

- **POST /upload**  
  Accepts a multipart‐form file upload. Saves into the working directory. Logs client IP and uploaded filename. Redirects back to `/` on success.

- **GET /monitor**  
  Returns metrics in JSON:
  ```json
  {
    "totalRequests": 123,
    "errorCount": 2
  }
  ```

---

## Logging

- **Console**: Every request, upload, download, and error is logged with timestamp and client IP.  
- **File**: Appends to `server.log` in the working directory.

_Log format:_
```
2025-06-12 12:34:56 [127.0.0.1] GET /download?file=test.zip
```

---

## Security Considerations

- **HTTPS**: Always use `--https` in production and bind a valid SSL cert.  
- **Authentication**: Basic Auth is low‐security; combine with HTTPS.  
- **Directory Permissions**: Ensure the working directory is writeable by the server but secure from unauthorized access.

---

