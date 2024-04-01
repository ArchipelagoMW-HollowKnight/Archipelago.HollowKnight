using System;

namespace Archipelago.HollowKnight
{
    internal class LoginValidationException : Exception
    {
        public LoginValidationException()
        {
        }

        public LoginValidationException(string message) : base(message)
        {
        }

        public LoginValidationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
