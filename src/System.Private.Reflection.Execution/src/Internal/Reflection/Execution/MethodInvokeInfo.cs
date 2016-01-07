// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
