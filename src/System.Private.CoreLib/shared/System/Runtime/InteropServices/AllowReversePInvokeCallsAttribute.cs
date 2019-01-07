// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////

namespace System.Runtime.InteropServices
{
   // To be used on methods that sink reverse P/Invoke calls.
    // This attribute is a CoreCLR-only security measure, currently ignored by the desktop CLR.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AllowReversePInvokeCallsAttribute : Attribute
    {
        public AllowReversePInvokeCallsAttribute()
        {
        }
    }
}