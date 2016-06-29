// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace Internal.Runtime
{
    // Extensions to EEType that are specific to the use in the CoreLib.
    unsafe partial struct EEType
    {
#if !INPLACE_RUNTIME
        internal EEType* GetArrayEEType()
        {

            return EETypePtr.EETypePtrOf<Array>().ToPointer();
        }
#endif
    }
}
