// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Win32 {
    using Microsoft.Win32;
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;
    using System.Runtime.Versioning;
    using System.Security;
    using System.Text;

    sealed internal class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid {
        internal SafeLibraryHandle() : base(true) {}

        override protected bool ReleaseHandle()
        {
            return UnsafeNativeMethods.FreeLibrary(handle);
        }
    }
}
