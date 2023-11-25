using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;


class Program
{
    static string webRoot = "";
    static void Main(string[] args)
    {
        string webRoot = "";
        string webIP = "";
        int webPort = 0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-webRoot":
                    if (i + 1 < args.Length) webRoot = args[++i];
                    break;
                case "-webIP":
                    if (i + 1 < args.Length) webIP = args[++i];
                    break;
                case "-webPort":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                    {
                        webPort = port;
                    }
                    break;
                default:
                    Log("[ERROR] - Missing required arguments (webRoot, webIP, webPort)");
                    break;
            }
        }

        if (string.IsNullOrEmpty(webRoot) || string.IsNullOrEmpty(webIP) || webPort == 0)
        {
            Log("[ERROR] - Missing required arguments (webRoot, webIP, webPort)");
            return;
        }

        Log("[SERVER STARTED] - Application started");


        // Assuming webIP and webPort are already set from the command line arguments
        IPAddress ipAddress = IPAddress.Parse(webIP);
        TcpListener listener = new TcpListener(ipAddress, webPort);

        try
        {
            listener.Start();
            Log("[SERVER STARTED] - Application started");

            while (true)
            {
                Log("[INFO] - Waiting for connection...");

                TcpClient client = listener.AcceptTcpClient();

                Log("[INFO] - Client connected.");

                // Handle the connection in a separate method
                HandleClient(client);

                client.Close();
            }
        }
        catch (Exception e)
        {
            Log($"[ERROR] - An exception occurred: {e.ToString()}");
        }

        finally
        {
            listener.Stop();
        }


    }
    static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();

        try
        {
            // Buffer for reading data
            byte[] bytes = new byte[1024];
            int numBytes;
            string requestData = "";

            // Read incoming stream
            while ((numBytes = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                requestData += Encoding.ASCII.GetString(bytes, 0, numBytes);

                // Check if end of the request
                if (requestData.EndsWith("\r\n\r\n"))
                    break;
            }

            Log($"[REQUEST] - {requestData.Replace("\r\n", " ")}"); // Log the request

            // Handle GET request
            if (requestData.StartsWith("GET"))
            {
                string url = requestData.Split(' ')[1];
                ServeFile(url, stream);
            }
            else
            {
                // Send response for unsupported methods
                SendErrorResponse(stream, "HTTP/1.1 405 Method Not Allowed\r\n\r\n");
                Log("[RESPONSE] - 405 Method Not Allowed"); // Log this response
            }
        }
        catch (Exception ex)
        {
            Log($"[ERROR] - Server encountered an error: {ex.Message}");
            SendErrorResponse(stream, "HTTP/1.1 500 Internal Server Error\r\n\r\n");
        }
        finally
        {
            client.Close();
        }
    }
    static void SendErrorResponse(NetworkStream stream, string responseStatus)
    {
        string response = responseStatus;
        byte[] msg = Encoding.ASCII.GetBytes(response);
        stream.Write(msg, 0, msg.Length);
        Log($"[RESPONSE] - {responseStatus.Trim()}");
    }
    static void ServeFile(string url, NetworkStream stream)
    {
        string filePath = webRoot + url;

        if (!File.Exists(filePath))
        {
            // Send 404 Not Found response
            SendErrorResponse(stream, "HTTP/1.1 404 Not Found\r\n\r\n");
            Log("[RESPONSE] - 404 Not Found");
            return;
        }

        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string contentType = GetContentType(filePath);

            // Building the response header
            StringBuilder header = new StringBuilder();
            header.AppendLine("HTTP/1.1 200 OK");
            header.AppendLine($"Content-Length: {fileBytes.Length}");
            header.AppendLine($"Content-Type: {contentType}");
            header.AppendLine($"Date: {DateTime.Now:R}"); // RFC1123 format
            header.AppendLine("Server: myOwnWebServer");

            // Send header
            byte[] headerBytes = Encoding.ASCII.GetBytes(header.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);

            // Send file
            stream.Write(fileBytes, 0, fileBytes.Length);

            // Logging
            Log($"[RESPONSE] - 200 OK Content-Type: {contentType} Content-Length: {fileBytes.Length}");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] - Internal Server Error: {ex.Message}");
            SendErrorResponse(stream, "HTTP/1.1 500 Internal Server Error\r\n\r\n");
        }
    }
    static string GetContentType(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        switch (ext)
        {
            case ".html":
                return "text/html";
            case ".txt":
                return "text/plain";
            case ".jpg":
                return "image/jpeg";
            case ".gif":
                return "image/gif";
            default:
                return "application/octet-stream";
        }
    }
    static void Log(string message)
    {
        string logFilePath = "myOwnWebServer.log";
        string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
    
            File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        
    }

}
