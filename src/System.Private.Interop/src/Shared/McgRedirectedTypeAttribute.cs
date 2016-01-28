// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Enum | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class McgRedirectedTypeAttribute : System.Attribute
    {
        public McgRedirectedTypeAttribute(string assemblyQualifiedTypeName) { }
    }
}
