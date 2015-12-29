// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class McgRedirectedMethodAttribute : System.Attribute
    {
        public McgRedirectedMethodAttribute(string assemblyQualifiedTypeName, string methodName) { }
    }
}
