using System;
using System.Runtime.Serialization;

namespace Archipelago.HollowKnight
{
    [Serializable]
    internal class ArchipelagoConnectionException : Exception
    {
        public ArchipelagoConnectionException()
        {
        }

        public ArchipelagoConnectionException(string message) : base(message)
        {
        }

        public ArchipelagoConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ArchipelagoConnectionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}