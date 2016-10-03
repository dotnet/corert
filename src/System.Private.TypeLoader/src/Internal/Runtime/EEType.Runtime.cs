// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Internal.Runtime.TypeLoader;

namespace Internal.Runtime
{
    // Supplies type loader specific extentions to EEType
    partial struct EEType
    {
        private unsafe static EEType* GetArrayEEType()
        {
            return ToEETypePtr(typeof(Array).TypeHandle);
        }

        private static unsafe EEType* ToEETypePtr(RuntimeTypeHandle rtth)
        {
            return (EEType*)(*(IntPtr*)&rtth);
        }
    }
}
