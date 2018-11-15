using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Client : Transport
    {
        /// <summary>
        /// The connection to the server
        /// </summary>
        public Connection connection;

        Thread thread;

        /// <summary>
        /// Creates a Tcp client with the default connection
        /// </summary>
        public Client()
        {
            connection = new Connection();
        }

        /// <summary>
        /// Creates a Tcp client with a user provided connection
        /// </summary>
        /// <param name="connection">Connection object to be used</param>
        public Client(Connection connection)
        {
            this.connection = connection;
        }

        /// <summary>
        /// Determines if we are connected to the server
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        public bool Connected
        {
            get
            {

                // TcpClient.Connected doesn't check if socket != null, which
                // results in NullReferenceExceptions if connection was closed.
                // -> let's check it manually instead
                return connection.status == Connection.Status.Connected;
            }
        }

        public bool NoDelay = true;

        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Telepathy.Client"/> is connecting.
        /// </summary>
        /// <value><c>true</c> if connecting; otherwise, <c>false</c>.</value>
        public bool Connecting 
        { 
            get 
            {
                return connection.status == Connection.Status.Connected;
            } 
        }

        // the thread function
        void ThreadFunction(string ip, int port)
        {
            // absolutely must wrap with try/catch, otherwise thread
            // exceptions are silent
            try
            {
                connection.tcpClient.Connect(ip, port);

                connection.status = Connection.Status.Connected;

                ProcessMessages(0, connection);
            }
            catch (Exception exception)
            {
                // this happens if (for example) the ip address is correct
                // but there is no server running on that ip/port
                Logger.Log("Client: failed to connect to ip=" + ip + " port=" + port + " reason=" + exception);

                // add 'Disconnected' event to message queue so that the caller
                // knows that the Connect failed. otherwise they will never know
                messageQueue.Enqueue(new ErrorMessage(0, exception));

                connection.status = Connection.Status.Disconnected;
            }
        }

        public void Connect(string ip, int port)
        {
            // not if already started
            if (connection.status != Connection.Status.Disconnected)
                return;


            connection.status = Connection.Status.Connecting;

            // clear old messages in queue, just to be sure that the caller
            // doesn't receive data from last time and gets out of sync.
            // -> calling this in Disconnect isn't smart because the caller may
            //    still want to process all the latest messages afterwards
            messageQueue.Clear();

            TcpClient tcpClient = new TcpClient();
            tcpClient.NoDelay = NoDelay;
            connection.tcpClient = tcpClient;

            // client.Connect(ip, port) is blocking. let's call it in the thread
            // and return immediately.
            // -> this way the application doesn't hang for 30s if connect takes
            //    too long, which is especially good in games
            // -> this way we don't async client.BeginConnect, which seems to
            //    fail sometimes if we connect too many clients too fast
            thread = new Thread(() => { ThreadFunction(ip, port); });
            thread.IsBackground = true;
            thread.Start();
        }

        public virtual void Disconnect()
        {
            connection.Close();

            // wait until thread finished. this is the only way to guarantee
            // that we can call Connect() again immediately after Disconnect
            if (thread != null)
                thread.Join();
        }

        public void Send(byte[] data)
        {
            if (Connected)
            {
                connection.SendMessage(data);
            }
            else
            {
                throw new IOException("Cannot send message, client not connected");
            }
        }
    }
}
