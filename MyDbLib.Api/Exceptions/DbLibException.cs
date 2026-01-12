using System;

namespace MyDbLib.Api.Exceptions
{
    public class DbLibException : Exception
    {
        public DbLibException(string message) : base(message) { }
        public DbLibException(string message, Exception inner)
            : base(message, inner) { }
    }
}
