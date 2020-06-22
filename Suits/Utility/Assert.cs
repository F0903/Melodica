using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Utility
{
    public static class Assert
    {
        class AssertionException : Exception
        {
            public AssertionException(string? msg = "Assertion failed.") : base(msg)
            { }
        }

        public static void NotNull(object? obj)
        {
            if (obj == null) throw new AssertionException();
        }

        public static void IsTrue(bool condition)
        {
            if (!condition) throw new AssertionException();
        }

        public static void IsFalse(bool condition) => IsTrue(!condition);
    }
}
