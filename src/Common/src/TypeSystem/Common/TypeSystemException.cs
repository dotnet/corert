// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Base type for all type system exceptions.
    /// </summary>
    public abstract class TypeSystemException : Exception
    {
        public TypeSystemException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// The exception that is thrown when type-loading failures occur.
        /// </summary>
        public class TypeLoadException : TypeSystemException
        {
            internal TypeLoadException(string message)
                : base(message)
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when there is an attempt to access a class member that does not exist
        /// or that is not declared as public.
        /// </summary>
        public abstract class MissingMemberException : TypeSystemException
        {
            protected internal MissingMemberException(string message)
                : base(message)
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when there is an attempt to access a method that does not exist.
        /// </summary>
        public class MissingMethodException : MissingMemberException
        {
            public MissingMethodException(string message)
                : base(message)
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when there is an attempt to access a field that does not exist.
        /// </summary>
        public class MissingFieldException : MissingMemberException
        {
            public MissingFieldException(string message)
                : base(message)
            {
            }
        }
    }
}
