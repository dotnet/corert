// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    [TypeForwardedFrom("System.Core, Version=3.5.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089")]
    public class LockRecursionException : System.Exception
    {
        public LockRecursionException() { }
        public LockRecursionException(string message) : base(message) { }
        public LockRecursionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
