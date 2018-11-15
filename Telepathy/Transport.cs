// common code used by server and client
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Telepathy
{
    /// <summary>
    /// Send and receive messages
    /// </summary>
    public abstract class Transport
    {
        // common code /////////////////////////////////////////////////////////
        // incoming message queue of <connectionId, message>
        // (not a HashSet because one connection can have multiple new messages)
        protected SafeQueue<Message> messageQueue = new SafeQueue<Message>();

        // warning if message queue gets too big
        // 
        /// <summary>
        /// Warn if message queue is larger than this number
        /// </summary>
        /// <remarks>
        /// if the average message is about 20 bytes then:
        /// -   1k messages are   20KB
        /// -  10k messages are  200KB
        /// - 100k messages are 1.95MB
        /// 2MB are not that much, but it is a bad sign if the caller process
        /// can't call GetNextMessage faster than the incoming messages.
        /// </remarks>
        public static int messageQueueSizeWarning = 100000;

        /// <summary>
        /// Reads the next message received
        /// </summary>
        /// <returns>The next message,  null if none available</returns>
        public virtual Message GetNextMessage()
        {
            Message message = null;
            messageQueue.TryDequeue(out message);
            return message;
        }

        // thread receive function is the same for client and server's clients
        protected virtual void ProcessMessages(int connectionId, Connection connection)
        {
            connection.OnConnect += () =>
            {
                Enqueue(new ConnectMessage(connectionId));
            };

            connection.OnData += (data) =>
            {
                Enqueue(new DataMessage(connectionId, data));
            };

            connection.OnDisconnect += () =>
            {
                Enqueue(new DisconnectMessage(connectionId));
            };

            connection.OnError += (exception) =>
            {
                Enqueue(new ErrorMessage(connectionId, exception));
            };

            connection.ProcessMessages();
        }

        // keep track of last message queue warning
        DateTime messageQueueLastWarning = DateTime.Now;

        private void Enqueue(Message message)
        {
            messageQueue.Enqueue(message);

            if (messageQueue.Count > messageQueueSizeWarning)
            {
                TimeSpan elapsed = DateTime.Now - messageQueueLastWarning;
                if (elapsed.TotalSeconds > 10)
                {
                    Logger.LogWarning("ReceiveLoop: messageQueue is getting big(" + messageQueue.Count + "), try calling GetNextMessage more often. You can call it more than once per frame!");
                    messageQueueLastWarning = DateTime.Now;
                }
            }
        }
    }
}
