using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    public class Connection
    {
        /// <summary>
        /// Connection life cycle
        /// </summary>
        public enum Status
        {
            Connecting,
            Connected,
            Disconnected,
        }

        /// <summary>
        /// Connection life cycle, 
        /// </summary>
        public volatile Status status;

        public event Action<byte[]> OnData;
        public event Action OnConnect;
        public event Action OnDisconnect;
        public event Action<Exception> OnError;

        public TcpClient tcpClient;

        protected virtual Stream stream
        {
            get
            {
                return tcpClient.GetStream();
            }
        }

        public Connection()
        {
            status = Status.Disconnected;
        }

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <param name="content">bytes to send as a message</param>
        /// <exception cref="IOException">If we fail to send the message</exception>
        public void SendMessage(byte[] content)
        {
            // construct header (size)

            // write header+content at once via payload array. writing
            // header,payload separately would cause 2 TCP packets to be
            // sent if nagle's algorithm is disabled(2x TCP header overhead)
            byte[] payload = new byte[sizeof(int) + content.Length];
            AddHeaderSize(payload, content.Length);
            Array.Copy(content, 0, payload, sizeof(int), content.Length);
            stream.Write(payload, 0, payload.Length);
        }

        void AddHeaderSize(byte[] payload, int value)
        {
            payload[0] = (byte)value;
            payload[1] = (byte)(value >> 8);
            payload[2] = (byte)(value >> 16);
            payload[3] = (byte)(value >> 24);
        }

        /// <summary>
        /// Reads a message from the other end
        /// </summary>
        /// <returns>The message  or null if end of file</returns>
        /// <exception cref="System.IO.IOException">if any any error occurs</exception>
        public byte[] ReadMessage()
        {
            // read exactly 4 bytes for header (blocking)
            byte[] header = stream.ReadExactly(4);
            if (header == null)
                return null;

            int size = BytesToInt(header);

            // read exactly 'size' bytes for content (blocking)
            byte[] content = stream.ReadExactly(size);
            if (content == null)
                throw new IOException("Invalid message received");

            return content;
        }

        static int BytesToInt(byte[] bytes)
        {
            return
                bytes[0] |
                (bytes[1] << 8) |
                (bytes[2] << 16) |
                (bytes[3] << 24);

        }

        // thread receive function is the same for client and server's clients
        // (static to reduce state for maximum reliability)
        public virtual void ProcessMessages()
        {
            // absolutely must wrap with try/catch, otherwise thread exceptions
            // are silent
            try
            {
                var onConnectTmp = OnConnect;
                if (onConnectTmp != null)
                    OnConnect();

                // let's talk about reading data.
                // -> normally we would read as much as possible and then
                //    extract as many <size,content>,<size,content> messages
                //    as we received this time. this is really complicated
                //    and expensive to do though
                // -> instead we use a trick:
                //      Read(4) -> size
                //        Read(size) -> content
                //      repeat
                //    Read is blocking, but it doesn't matter since the
                //    best thing to do until the full message arrives,
                //    is to wait.
                // => this is the most elegant AND fast solution.
                //    + no resizing
                //    + no extra allocations, just one for the content
                //    + no crazy extraction logic
                while (tcpClient.Connected)
                {
                    // read the next message (blocking) or stop if stream closed
                    byte[] content = ReadMessage();
                    if (content == null)
                        break;

                    // thread safe way to raise events
                    var onDataTmp = OnData;
                    if (onDataTmp != null)
                        onDataTmp(content);
                }
            }
            catch (Exception exception)
            {
                // something went wrong. the thread was interrupted or the
                // connection closed or we closed our own connection or ...
                // -> either way we should stop gracefully
                var onErrorTmp = OnError;
                if (onErrorTmp != null)
                    onErrorTmp(exception);
            }
            finally
            {
                Close();

                var onDisconnectTmp = OnDisconnect;
                if (onDisconnectTmp != null)
                    onDisconnectTmp();

            }
        }

        public void Close()
        {
            // clean up no matter what
            if (stream != null)
            {
                // eat any error
                try { stream.Close(); } catch (Exception) { }
            }

            if (tcpClient != null)
            {
                // eat any error
                try { tcpClient.Close(); } catch (Exception) { }
            }
            status = Status.Disconnected;

        }

        public void Stop()
        {
            Close();
        }
    }
}
