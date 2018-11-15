// incoming message queue of <connectionId, message>
// (not a HashSet because one connection can have multiple new messages)
namespace Telepathy
{
    /// <summary>
    /// Message received in a connection
    /// </summary>
    public struct Message
    {
        /// <summary>
        /// id of the connection that received the message
        /// </summary>
        public int connectionId;
        public EventType eventType;
        public byte[] data;
        public Message(int connectionId, EventType eventType, byte[] data)
        {
            this.connectionId = connectionId;
            this.eventType = eventType;
            this.data = data;
        }
    }
}