// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
