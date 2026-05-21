using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CompressionServer
{
    class Program
    {
       
        private const int PORT = 9000;
        private const int BUFFER_SIZE = 8192;   

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            TcpListener listener = new TcpListener(IPAddress.Any, PORT);
            listener.Start();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║      Multi-threaded Compression Server   ║");
            Console.WriteLine($"║   Port: {PORT}  |  Waiting for clients...  ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.ResetColor();

            while (true)
            {
                
                TcpClient client = listener.AcceptTcpClient();

                string clientEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                Console.WriteLine($"\n[+] Client connected: {clientEndPoint}  [{DateTime.Now:HH:mm:ss}]");

                
                Thread clientThread = new Thread(() => HandleClient(client, clientEndPoint));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }

        
        static void HandleClient(TcpClient client, string endPoint)
        {
            try
            {
                using (client)
                using (NetworkStream netStream = client.GetStream())
                {
                    
                    long fileSize = ReadInt64(netStream);
                    Console.WriteLine($"  [{endPoint}] Original size  : {FormatBytes(fileSize)}");

                    
                    byte[] fileData = ReadExactBytes(netStream, fileSize);
                    Console.WriteLine($"  [{endPoint}] File received successfully.");

                    
                    byte[] compressedData = CompressData(fileData);
                    Console.WriteLine($"  [{endPoint}] Compressed size : {FormatBytes(compressedData.Length)}");

                    double ratio = (1.0 - (double)compressedData.Length / fileSize) * 100;
                    Console.WriteLine($"  [{endPoint}] Compression     : {ratio:F1}%");

                   
                    WriteInt64(netStream, compressedData.Length);

                   
                    netStream.Write(compressedData, 0, compressedData.Length);
                    netStream.Flush();

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  [{endPoint}] Compressed file sent successfully.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [{endPoint}] Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        
        static byte[] CompressData(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    gzip.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }

        
        static long ReadInt64(NetworkStream stream)
        {
            byte[] buf = ReadExactBytes(stream, 8);
            if (BitConverter.IsLittleEndian) Array.Reverse(buf);
            return BitConverter.ToInt64(buf, 0);
        }

    
       static void WriteInt64(NetworkStream stream, long value)
        {
            byte[] buf = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian) Array.Reverse(buf);
            stream.Write(buf, 0, buf.Length);
        }

        
        static byte[] ReadExactBytes(NetworkStream stream, long count)
        {
            byte[] result = new byte[count];
            long received = 0;

            while (received < count)
            {
                int toRead = (int)Math.Min(BUFFER_SIZE, count - received);
                int read = stream.Read(result, (int)received, toRead);
                if (read == 0) throw new EndOfStreamException("Connection lost before all data was received.");
                received += read;
            }
            return result;
        }

        
        static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F2} KB";
            return $"{bytes / (1024.0 * 1024):F2} MB";
        }
    }
}