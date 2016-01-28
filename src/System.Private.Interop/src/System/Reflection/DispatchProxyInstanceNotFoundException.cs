// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Reflection
{
    /// <summary>
    /// Exception thrown if we cannot find an dispatch proxy instance type for a requested interface
    /// and proxy class type.
    /// </summary>
    public class DispatchProxyInstanceNotFoundException : System.Exception
    {
        public DispatchProxyInstanceNotFoundException()
        {
        }

        public DispatchProxyInstanceNotFoundException(string message) :
            base(message)
        {
        }

        public DispatchProxyInstanceNotFoundException(string message, Exception inner) :
            base(message, inner)
        {
        }
    }
}
