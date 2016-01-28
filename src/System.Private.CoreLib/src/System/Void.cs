// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 

////////////////////////////////////////////////////////////////////////////////
// Void
//    This class represents the void return type
////////////////////////////////////////////////////////////////////////////////

namespace System
{
    // This class represents the void return type
    // For RH, this type wont be available at runtime
    // typeof(void) would fail.
    [System.Runtime.InteropServices.ComVisible(true)]
    public struct Void
    {
    }
}
