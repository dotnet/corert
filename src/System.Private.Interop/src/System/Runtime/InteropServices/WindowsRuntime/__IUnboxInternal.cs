// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    /// <summary>
    /// Interface to help get unboxed value from IReference<T>/IReferenceArray<T>/IKeyValuePair<K,V>
    /// </summary>
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    internal interface __IUnboxInternal
    {
        Object get_Value(Object obj);
    }
}
