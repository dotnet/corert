// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Delegate | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class McgComCallableAttribute : System.Attribute
    {
        public McgComCallableAttribute() { }
    }
}
