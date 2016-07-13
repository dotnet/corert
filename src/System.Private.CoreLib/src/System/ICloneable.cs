// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System
{
    /// <summary>Defines an interface indicating that an object may be cloned.</summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface ICloneable
    {
        object Clone();
    }
}
