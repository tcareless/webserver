/*
    ==============================================================================
    |                 myOwnWebServer - C# Web Server Application                 |
    ==============================================================================
    
    Author: Tyler Careless
    Student ID: 8857054
    Class: WDD (Web Design and Development)
    Assignment: A05 - Developing Your Own WebServer
    Date: 2023-11-25

    Description:
    This application is a single-threaded web server. It is capable of handling
    GET requests and serving text, HTML, JPG, and GIF content types.

    The server accepts command-line arguments to set up the root directory for web
    content (-webRoot), the IP address to bind to (-webIP), and the port number 
    to listen on (-webPort). It logs all server activity to 'myOwnWebServer.log'.

    Usage:
    myOwnWebServer.exe -webRoot <path_to_web_content> -webIP <server_ip> -webPort <port_number>

    Example:
    myOwnWebServer.exe -webRoot "C:\webserver\content" -webIP "127.0.0.1" -webPort 5000

    ==============================================================================
*/
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

    /// <summary>
    /// Handles an individual client connection.
    /// </summary>
    /// <param name="client">The TCP client to handle.</param>
    /// This method processes an incoming client request by reading data from the network stream,
    /// logging the request, determining the type of HTTP method, and calling the appropriate handler.
    /// It supports only GET requests and responds with a 405 Method Not Allowed for all other methods.
    /// If any exceptions occur during the request handling, it logs the error and sends a 500 Internal
    /// Server Error response. The client connection is closed after handling the request.
    static void HandleClient(TcpClient client)
    {
        // Obtain the network stream associated with the client
        NetworkStream stream = client.GetStream();

        try
        {
            // Buffer for reading data
            byte[] bytes = new byte[1024];
            int numBytes; // Number of bytes read in one Read operation
            string requestData = ""; // String to hold the request data

            // Read the incoming stream in a loop
            while ((numBytes = stream.Read(bytes, 0, bytes.Length)) != 0)
            {
                // Append the read data to the request string
                requestData += Encoding.ASCII.GetString(bytes, 0, numBytes);

                // Check if the end of the request has been reached (indicated by double CRLF)
                if (requestData.EndsWith("\r\n\r\n"))
                    break;
            }

            // Log the incoming HTTP request data
            Log($"[REQUEST] - {requestData.Replace("\r\n", " ")}");

            // Check if the request starts with 'GET' and handle it
            if (requestData.StartsWith("GET"))
            {
                // Extract the URL from the request data
                string url = requestData.Split(' ')[1];
                // Serve the requested file
                ServeFile(url, stream);
            }
            else
            {
                // If the method is not GET, send a 405 Method Not Allowed response
                SendErrorResponse(stream, "HTTP/1.1 405 Method Not Allowed\r\n\r\n");
                // Log the response
                Log("[RESPONSE] - 405 Method Not Allowed");
            }
        }
        catch (Exception ex)
        {
            // Log any exceptions encountered during request handling
            Log($"[ERROR] - Server encountered an error: {ex.Message}");
            // Send a 500 Internal Server Error response
            SendErrorResponse(stream, "HTTP/1.1 500 Internal Server Error\r\n\r\n");
        }
        finally
        {
            // Close the client connection
            client.Close();
        }
    }

    /// <summary>
    /// Sends an HTTP error response to the client.
    /// </summary>
    /// <param name="stream">The network stream to send the response to.</param>
    /// <param name="responseStatus">The HTTP status line to send as part of the response header.</param>
    /// This method is used to send error status responses to the client. It constructs an HTTP response using
    /// the provided response status line, sends it over the given network stream, and logs the action. It is
    /// typically used to handle client errors (4xx) and server errors (5xx).
    static void SendErrorResponse(NetworkStream stream, string responseStatus)
    {
        // The full HTTP response string including the status line
        string response = responseStatus;

        // Convert the response string to a byte array using ASCII encoding
        byte[] msg = Encoding.ASCII.GetBytes(response);

        // Write the error message to the stream
        stream.Write(msg, 0, msg.Length);

        // Log the response status that is being sent to the client
        Log($"[RESPONSE] - {responseStatus.Trim()}");
    }

    /// <summary>
    /// Serves a file over an established network stream.
    /// </summary>
    /// <param name="url">The URL path to the requested file.</param>
    /// <param name="stream">The network stream through which data should be sent.</param>
    /// This method attempts to serve the requested file specified by the URL. If the file does not exist,
    /// a 404 Not Found response is sent. If the file exists, it reads the file into a byte array, builds a proper
    /// HTTP response header, and writes both header and file content to the network stream. It logs all
    /// actions and catches any exceptions, logging them and sending a 500 Internal Server Error response if needed.
    static void ServeFile(string url, NetworkStream stream)
    {
        // Combine the web root path with the requested URL to form the file path
        string filePath = webRoot + url;

        // Check if the requested file exists at the specified path
        if (!File.Exists(filePath))
        {
            // If not, send a 404 Not Found response and log the event
            SendErrorResponse(stream, "HTTP/1.1 404 Not Found\r\n\r\n");
            Log("[RESPONSE] - 404 Not Found");
            return;
        }

        try
        {
            // Read the entire file into a byte array
            byte[] fileBytes = File.ReadAllBytes(filePath);
            // Determine the content type based on the file extension
            string contentType = GetContentType(filePath);

            // Build the HTTP response header
            StringBuilder header = new StringBuilder();
            header.AppendLine("HTTP/1.1 200 OK");
            header.AppendLine($"Content-Length: {fileBytes.Length}");
            header.AppendLine($"Content-Type: {contentType}");
            header.AppendLine($"Date: {DateTime.Now:R}"); // Use RFC1123 format for the date
            header.AppendLine("Server: myOwnWebServer");

            // Convert the header to a byte array and send it
            byte[] headerBytes = Encoding.ASCII.GetBytes(header.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);

            // Send the file content
            stream.Write(fileBytes, 0, fileBytes.Length);

            // Log the successful response
            Log($"[RESPONSE] - 200 OK Content-Type: {contentType} Content-Length: {fileBytes.Length}");
        }
        catch (Exception ex)
        {
            // Log any exceptions during the process and send a 500 Internal Server Error response
            Log($"[ERROR] - Internal Server Error: {ex.Message}");
            SendErrorResponse(stream, "HTTP/1.1 500 Internal Server Error\r\n\r\n");
        }
    }

    /// <summary>
    /// Determines the MIME content type based on the file extension.
    /// </summary>
    /// <param name="path">The file path from which to extract the content type.</param>
    /// <returns>A string representing the MIME type of the given file.</returns>
    /// This method uses the file extension to map to a known MIME type.
    /// If the extension is not recognized, it defaults to 'application/octet-stream',
    /// which is a generic binary file that could be any type of data.
    /// Recognized file types are HTML, plain text, JPEG, and GIF.
    static string GetContentType(string path)
    {
        // Extract the file extension in lowercase from the provided path
        string ext = Path.GetExtension(path).ToLower();

        // Use a switch statement to match the file extension to the MIME type
        switch (ext)
        {
            case ".html":
                // Return MIME type for HTML files
                return "text/html";
            case ".txt":
                // Return MIME type for plain text files
                return "text/plain";
            case ".jpg":
                // Return MIME type for JPEG images
                return "image/jpeg";
            case ".gif":
                // Return MIME type for GIF images
                return "image/gif";
            default:
                // Return a default MIME type for any unrecognized file extension
                return "application/octet-stream";
        }
    }
    /// </summary>
    /// Logs a message to the "myOwnWebServer.log" file with a timestamp.
    /// <param name="message">The message to be logged.</param>
    /// This method appends a new line to the log file with the current date and time 
    /// followed by the message passed as a parameter. If the log file does not exist, it is created.
    static void Log(string message)
    {
        // Define the path for the log file
        string logFilePath = "myOwnWebServer.log";
        // Create a log entry with the current timestamp and the message
        string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";

        // Append the log entry to the file, adding a newline character to separate entries
        File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
    }


}
