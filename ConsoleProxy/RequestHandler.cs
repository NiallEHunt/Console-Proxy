using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ConsoleProxy
{
    class RequestHandler
    {
        private const int BufferSize = 8192;

        // Takes a client and a request and handles the request.
        // Will check if the request is HTTP or HTTPS and deal with it appropriately.
        public static void HandleRequest(TcpClient inClient, string[] request)
        {
            // Creates a header with the request. This is just a small class to seperate out the hostname,
            // port and other parts from the request and present it nicely.
            Header header = new Header(request);

            StringBuilder builder = new StringBuilder();

            foreach (var item in request)
            {
                builder.Append(item + " ");
            }
            builder.Append("\r\n");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(builder.ToString());
            Console.ForegroundColor = ConsoleColor.Green;

            // If the firewall is active and the requested host is banned then a 403 will be sent back as a reponse
            // otherwise the request type is ussed to see if it is HTTP or HTTPS and the correct method is then used.
            if (!Firewall.IsActive || Firewall.isAllowed(header.Hostname))
            {
                try
                {
                    // If the request type is CONNECT then it is HTTPs
                    if (header.RequestType == "CONNECT")
                    {
                        TcpClient outClient = new TcpClient(header.Hostname, header.Port);
                        HandleHttpsRequest(inClient, outClient, header);
                    }
                    else
                    {
                        HandleHttpRequest(inClient, header);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            else
            {
                SendConnectionForbidden(inClient.GetStream(), header);
            }
        }

        // Handles HTTPS requests by creating a TcpTunnel to connect the inClient (the client who sent the request) to the outClient (the host).
        // The tunnel just passes the encrypted data from one client to the other using TCP.
        private static async void HandleHttpsRequest(TcpClient inClient, TcpClient outClient, Header header)
        {
            try
            {
                NetworkStream inClientStream = inClient.GetStream();
                NetworkStream outClientStream = outClient.GetStream();
                
                using (var tunnel = new TcpTunnel())
                {
                    var task = tunnel.Run(inClientStream, outClientStream);
                    await SendConnectionEstablished(inClientStream, header);
                    await task;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // Returns a 200 Connection established response to the client if a successful connection is made with the host.
        private static async Task SendConnectionEstablished(Stream stream, Header header)
        {
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }

        // Used when the site is blocked by the firewall to respond with a 403 Connection forbidden response.
        private static async void SendConnectionForbidden(Stream stream, Header header)
        {
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 403 Connection forbidden\r\n\r\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Firewall has banned " + header.Hostname + ":" + header.Port);
            Console.ForegroundColor = ConsoleColor.Green;
        }

        // This method handles the HTTP request. It first checks the cache to see if the correct response has been cached.
        // If not the response is gotten from the host and saved in the cache if possible.
        private static void HandleHttpRequest(TcpClient client, Header header)
        {
            DateTime start = DateTime.Now;
            var request = WebRequest.CreateHttp(header.Hostname);

            // Will return null if the response isn't found in the cache.
            CacheData cacheEntry = Cache.GetData(request);

            // The response hasn't been found in the cache and so we need to get it from the host and then store it in the cache
            if (cacheEntry == null)
            {
                HttpWebResponse response;
                Stream outStream = client.GetStream();

                // Gets the response from the host
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                }
                catch (WebException webEx)
                {
                    response = webEx.Response as HttpWebResponse;
                }

                // If the response isn't null send it to the client and add it to the cache
                if (response != null)
                {
                    List<Tuple<String, String>> responseHeaders = ProcessResponse(response);
                    StreamWriter responseWriter = new StreamWriter(outStream);
                    Stream responseStream = response.GetResponseStream();
                    MemoryStream cacheStream = null;

                    try
                    {
                        // Send the response to the client
                        WriteResponseStatus(response.StatusCode, response.StatusDescription, responseWriter);
                        WriteResponseHeaders(responseWriter, responseHeaders);

                        // If you can cache the response create a CacheData entry for the response
                        DateTime? expires = null;
                        CacheData entry = null;
                        Boolean canCache = (Cache.CanCache(response.Headers, ref expires));
                        if (canCache)
                        {
                            entry = Cache.AddDataEntry(request, response, responseHeaders, expires);
                            if (response.ContentLength > 0)
                                cacheStream = new MemoryStream(entry.ResponseBytes);
                        }

                        Byte[] buffer;
                        if (response.ContentLength > 0)
                            buffer = new Byte[response.ContentLength];
                        else
                            buffer = new Byte[BufferSize];

                        int bytesRead;

                        while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (cacheStream != null)
                                cacheStream.Write(buffer, 0, bytesRead);
                            outStream.Write(buffer, 0, bytesRead);
                        }
                        responseStream.Close();

                        if (cacheStream != null)
                        {
                            cacheStream.Flush();
                            cacheStream.Close();
                        }
                        outStream.Flush();

                        // Add the cache data entry to the cache
                        if (canCache)
                            Cache.AddData(entry);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    finally
                    {
                        responseStream.Close();
                        response.Close();
                        responseWriter.Close();
                    }
                }
            }
            // If the cache response isn't null we have the response already cached and so use this to respond to the client
            else
            {
                Stream outStream = client.GetStream();
                Console.WriteLine("Response from Cache");
                Console.WriteLine(String.Format("Saved {0} bytes of bandwidth by caching this response", cacheEntry.ResponseBytes.Length));
                StreamWriter responseWriter = new StreamWriter(outStream);
                try
                {
                    // Writes the response from the cache entry
                    WriteResponseStatus(cacheEntry.StatusCode, cacheEntry.StatusDescription, responseWriter);
                    WriteResponseHeaders(responseWriter, cacheEntry.Headers);
                    if (cacheEntry.ResponseBytes != null)
                    {
                        outStream.Write(cacheEntry.ResponseBytes, 0, cacheEntry.ResponseBytes.Length);
                    }
                    responseWriter.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    responseWriter.Close();
                }
            }

            // Used to time the request to show the efficiency of the cache
            DateTime end = DateTime.Now;
            Console.WriteLine("Request took " + (end - start).ToString());
        }

        // Responds with a HTTP Satus (Status code and Status description)
        private static void WriteResponseStatus(HttpStatusCode code, String description, StreamWriter responseWriter)
        {
            String s = String.Format("HTTP/1.0 {0} {1}", (Int32)code, description);
            responseWriter.WriteLine(s);
        }

        // Responds with the HTTP Headers 
        private static void WriteResponseHeaders(StreamWriter responseWriter, List<Tuple<String, String>> headers)
        {
            if (headers != null)
            {
                foreach (Tuple<String, String> header in headers)
                    responseWriter.WriteLine(String.Format("{0}: {1}", header.Item1, header.Item2));
            }
            responseWriter.WriteLine();
            responseWriter.Flush();
        }

        private static readonly Regex cookieSplitRegEx = new Regex(@",(?! )");

        // Used to process the response to see if there are cookies that need to be set
        private static List<Tuple<String, String>> ProcessResponse(HttpWebResponse response)
        {
            String value = null;
            String header = null;
            List<Tuple<String, String>> returnHeaders = new List<Tuple<String, String>>();
            foreach (String headerKeys in response.Headers.Keys)
            {
                if (headerKeys.ToLower() == "set-cookie")
                {
                    header = headerKeys;
                    value = response.Headers[headerKeys];
                }
                else
                    returnHeaders.Add(new Tuple<String, String>(headerKeys, response.Headers[headerKeys]));
            }

            if (!String.IsNullOrWhiteSpace(value))
            {
                response.Headers.Remove(header);
                String[] cookies = cookieSplitRegEx.Split(value);
                foreach (String cookie in cookies)
                    returnHeaders.Add(new Tuple<String, String>("Set-Cookie", cookie));

            }

            returnHeaders.Add(new Tuple<String, String>("X-Proxied-By", "niallehunt proxy"));
            return returnHeaders;
        }
    }
}
