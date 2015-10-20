// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
