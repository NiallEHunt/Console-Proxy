using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Collections.Generic;

namespace ConsoleProxy
{
    public sealed class ProxyServer
    {
        private TcpListener listener;
        private Thread listenerThread;
        private Thread cacheMaintenanceThread;
        private static bool isListening;

        public ProxyServer()
        {
            // Creates and initialises the listener, listener thread and the cache maintenance thread.
            listener = new TcpListener(IPAddress.Loopback, 7777);
            listenerThread = new Thread(new ParameterizedThreadStart(Listen));
            cacheMaintenanceThread = new Thread(new ThreadStart(Cache.CacheMaintenance));
        }

        public void Start()
        {
            // Starts the listener, listener thread and cahce maintenance thread.
            listener.Start();
            isListening = true;
            listenerThread.Start(listener);
            cacheMaintenanceThread.Start();
        }

        public void Stop()
        {
            // Called when shutting down the server.
            // Stops the listener and the two threads.
            listener.Stop();
            isListening = false;
            listenerThread.Abort();
            cacheMaintenanceThread.Abort();
            listenerThread.Join();
            cacheMaintenanceThread.Join();
        }

        private static void Listen(Object listenerObj)
        {
            // The function that the listener thread runs.
            // While the listener is listening it will accept TCP clients. When a client is accepted a work item is queued
            // to handle the TCP Request. This uses a thread from the thread pool when it is available to handle the request.
            TcpListener listener = (TcpListener)listenerObj;
            try
            {
                while (isListening)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    while (!ThreadPool.QueueUserWorkItem(new WaitCallback(ProxyServer.HandleTCPRequest), client))
                    {
                        Console.WriteLine("Failed to queue Request Processing thread");
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (SocketException) { }
        }

        // This is the function called to handle a TCP Request. This is run on a thread from the thread pool.
        // It gets the client stream and the request and calls the request handler with these.
        private static void HandleTCPRequest(Object clientObject)
        {
            TcpClient client = (TcpClient)clientObject;

            try
            {
                NetworkStream clientStream = client.GetStream();
                StreamReader clientReader = new StreamReader(clientStream);

                List<String> connectRequest = new List<string>();
                string line;

                // Build up the request string
                while (!String.IsNullOrEmpty(line = clientReader.ReadLine()))
                {
                    connectRequest.Add(line);
                }

                // If the request is empty throw an exception
                if(connectRequest.Count == 0)
                {
                    throw new Exception("Empty request: " + connectRequest.ToString());
                }

                string[] requestLine = connectRequest[0].Split(' ');

                // If the request is less than 3 words it is invalid and so throw an exception 
                if (requestLine.Length < 3)
                {
                    throw new Exception("Request is too short: " + requestLine.ToString());
                }

                RequestHandler.HandleRequest(client, requestLine);
            }
            // Used to catch any exceptions that occur from handling the request
            catch (Exception)
            {
                Console.WriteLine("Problem occured. Closing TCP Connection.");
                if(client.Connected)
                {
                    client.Close();
                }
            }
        }
    }
}
