// incoming message queue of <connectionId, message>
// (not a HashSet because one connection can have multiple new messages)
using System;

namespace Telepathy
{
    public class Message
    {
        public readonly int connectionId;
        public Message(int connectionId)
        {
            this.connectionId = connectionId;
        }
    }

    public class ErrorMessage : Message
    {
        public Exception exception;

        public ErrorMessage(int connectionId, Exception exception) : base(connectionId)
        {
            this.exception = exception;
        }
    }

    public class ConnectMessage : Message
    {
        public ConnectMessage(int connectionId) : base(connectionId)
        {
        }
    }

    public class DisconnectMessage : Message
    {
        public DisconnectMessage(int connectionId) : base(connectionId)
        {
        }
    }

    public class DataMessage : Message
    {
        public readonly byte[] data;

        public DataMessage(int connectionId, byte[] data) : base(connectionId)
        {
            this.data = data;
        }
    }

}