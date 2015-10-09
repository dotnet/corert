// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// 

// 

//
// Abstract derivations of SafeHandle designed to provide the common
// functionality supporting Win32 handles. More specifically, they describe how
// an invalid handle looks (for instance, some handles use -1 as an invalid
// handle value, others use 0).
//
// Further derivations of these classes can specialise this even further (e.g.
// file or registry handles).
// 
//

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Microsoft.Win32.SafeHandles
{
    // Class of safe handle which uses 0 or -1 as an invalid handle.
    [System.Security.SecurityCritical]  // auto-generated_required
    public abstract class SafeHandleZeroOrMinusOneIsInvalid : SafeHandle
    {
        protected SafeHandleZeroOrMinusOneIsInvalid(bool ownsHandle) : base(IntPtr.Zero, ownsHandle)
        {
        }

        public override bool IsInvalid
        {
            [System.Security.SecurityCritical]
            get
            { return DangerousGetHandle() == IntPtr.Zero || DangerousGetHandle() == new IntPtr(-1); }
        }
    }

    // Class of safe handle which uses only -1 as an invalid handle.
    [System.Security.SecurityCritical]  // auto-generated_required
    public abstract class SafeHandleMinusOneIsInvalid : SafeHandle
    {
        protected SafeHandleMinusOneIsInvalid(bool ownsHandle) : base(new IntPtr(-1), ownsHandle)
        {
        }

        public override bool IsInvalid
        {
            [System.Security.SecurityCritical]
            get
            { return DangerousGetHandle() == new IntPtr(-1); }
        }
    }
    //    // Class of critical handle which uses 0 or -1 as an invalid handle.
    //    [System.Security.SecurityCritical]  // auto-generated_required
    //    public abstract class CriticalHandleZeroOrMinusOneIsInvalid : CriticalHandle
    //    {
    //        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    //        protected CriticalHandleZeroOrMinusOneIsInvalid() : base(IntPtr.Zero) 
    //        {
    //        }

    //        public override bool IsInvalid {
    //            [System.Security.SecurityCritical]
    //            get { return handle.IsNull() || handle == new IntPtr(-1); }
    //        }
    //    }

    //    // Class of critical handle which uses only -1 as an invalid handle.
    //    [System.Security.SecurityCritical]  // auto-generated_required
    //#if !FEATURE_CORECLR
    //    [SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode=true)]
    //#endif
    //    public abstract class CriticalHandleMinusOneIsInvalid : CriticalHandle
    //    {
    //        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
    //        protected CriticalHandleMinusOneIsInvalid() : base(new IntPtr(-1)) 
    //        {
    //        }

    //        public override bool IsInvalid {
    //            [System.Security.SecurityCritical]
    //            get { return handle == new IntPtr(-1); }
    //        }
    //    }

}
