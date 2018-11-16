using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Server : Transport
    {
        // listener
        TcpListener listener;
        Thread listenerThread;

        // connectionId to connections
        SafeDictionary<int, Connection> connections = new SafeDictionary<int, Connection>();

        public bool NoDelay = true;
        public int MaxConnections = int.MaxValue;

        // connectionId counter
        int connectionId = 0;

        int NextConnectionId()
        {
            unchecked
            {
                // no problem if we overflow,  it will simply start from negatives
                // the only requirement is there is no more than one connection with the same id
                return Interlocked.Increment(ref connectionId);
            }
        }

        // check if the server is running
        public bool Active
        {
            get { return listenerThread != null && listenerThread.IsAlive; }
        }

        // the listener thread's listen function
        void Listen(int port)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                // start listener
                listener = new TcpListener(IPAddress.Any, port);
                // NoDelay disables nagle algorithm. lowers CPU% and latency
                // but increases bandwidth
                listener.Server.NoDelay = this.NoDelay;
                listener.Start();
                Logger.Log("Server: listening port=" + port);

                // keep accepting new clients
                while (true)
                {
                    Accept();
                }
            }
            catch (ThreadAbortException exception)
            {
                // UnityEditor causes AbortException if thread is still
                // running when we press Play again next time. that's okay.
                Logger.Log("Server thread aborted. That's okay. " + exception);
            }
            catch (SocketException exception)
            {
                // calling StopServer will interrupt this thread with a
                // 'SocketException: interrupted'. that's okay.
                Logger.LogDebug("Server Thread stopped. That's okay. " + exception);
            }
            catch (Exception exception)
            {
                messageQueue.Enqueue(new ErrorMessage(0, exception));
                // something went wrong. probably important.
                Logger.LogError("Server Exception: " + exception);
            }
            finally
            {
                // need to stop all connections
                foreach (Connection connection in connections.GetValues())
                {
                    connection.Stop();
                }
                connections.Clear();
            }
        }

        private void Accept()
        {
            // wait and accept new client
            // note: 'using' sucks here because it will try to
            // dispose after thread was started but we still need it
            // in the thread
            TcpClient client = listener.AcceptTcpClient();

            // are more connections allowed?
            if (connections.Count < MaxConnections)
            {
                // generate the next connection id (thread safely)
                int connectionId = NextConnectionId();

                Connection connection = new Connection();
                connection.tcpClient = client;
                connections.Add(connectionId, connection);

                // spawn a thread for each client to listen to his
                // messages
                connection.thread = new Thread(() =>
                {

                    try
                    {
                        // run the receive loop
                        ProcessMessages(connectionId, connection);

                    }
                    finally
                    {
                        // remove client from clients dict afterwards
                        connections.Remove(connectionId);
                    }
                });
                connection.thread.IsBackground = true;
                connection.thread.Start();
            }
            // connection limit reached. disconnect the client and show
            // a small log message so we know why it happened.
            // note: no extra Sleep because Accept is blocking anyway
            else
            {
                client.Close();
                Logger.Log("Server too full, disconnected a client");
            }
        }

        // start listening for new connections in a background thread and spawn
        // a new thread for each one.
        public void Start(int port)
        {
            // not if already started
            if (Active) return;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Stop isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            messageQueue.Clear();

            // start the listener thread
            Logger.Log("Server: Start port=" + port);
            listenerThread = new Thread(() => { Listen(port); });
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        public virtual void Stop()
        {
            // only if started
            if (!Active) return;

            Logger.Log("Server: stopping...");

            listener.Stop();
            listenerThread.Join();

        }

        // send message to client using socket connection.
        public void Send(int connectionId, byte[] data)
        {
            Connection connection;

            if (connections.TryGetValue(connectionId, out connection))
            {
                connection.SendMessage(data);
            }
            else
            {
                throw new IOException("Invalid connection " + connectionId);
            }
        }

        // disconnect (kick) a client
        public void Disconnect(int connectionId)
        {
            Connection connection;

            if (connections.TryGetValue(connectionId, out connection))
            {
                connection.Stop();
            }
        }
    }
}
