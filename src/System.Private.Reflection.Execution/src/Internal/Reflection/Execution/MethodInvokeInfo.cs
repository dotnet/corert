// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::Internal.Runtime.CompilerServices;

namespace Internal.Reflection.Execution
{
    internal sealed class MethodInvokeInfo
    {
        public IntPtr LdFtnResult { get; set; }
        public IntPtr DynamicInvokeMethod { get; set; }
        public IntPtr DynamicInvokeGenericDictionary { get; set; }
        public string DefaultValueString { get; set; }
        public IntPtr VirtualResolveData { get; set; }
    }
}
