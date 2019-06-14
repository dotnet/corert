// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Runtime.InteropServices.WindowsRuntime
{
    [ComImport]
    [Guid("61c17707-2d65-11e0-9ae8-d48564015472")]
    [WindowsRuntimeImport]
    // T can be any WinRT-compatible type, including reference types. 
    public interface IReferenceArray<T> : IPropertyValue
    {
        T[] get_Value();
    }
}
