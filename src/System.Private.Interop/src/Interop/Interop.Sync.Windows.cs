// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

//
// All P/invokes used by System.Private.Interop and MCG generated code goes here.
//
// !!IMPORTANT!!
//
// Do not rely on MCG to generate marshalling code for these p/invokes as MCG might not see them at all
// due to not seeing dependency to those calls (before the MCG generated code is generated). Instead,
// always manually marshal the arguments

using System;
using System.Runtime.CompilerServices;


namespace System.Runtime.InteropServices
{
    public static partial class ExternalInterop
    {
        internal struct CRITICAL_SECTION
        {
#pragma warning disable 169     // Field 'foo' is never used
            IntPtr DebugInfo;
            Int32 LockCount;
            Int32 RecursionCount;
            IntPtr OwningThread;
            IntPtr LockSemaphore;
            Int32 SpinCount;
#pragma warning restore 169
        }

        private static partial class Libraries
        {
#if TARGET_CORE_API_SET
            internal const string CORE_SYNCH_L1 = "api-ms-win-core-synch-l1-1-0.dll";
#else
            internal const string CORE_SYNCH_L1 = "kernel32.dll";
#endif
        }

        [DllImport(Libraries.CORE_SYNCH_L1)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe void InitializeCriticalSectionEx(CRITICAL_SECTION* lpCriticalSection, int dwSpinCount, int flags);

        [DllImport(Libraries.CORE_SYNCH_L1)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe void EnterCriticalSection(CRITICAL_SECTION* lpCriticalSection);

        [DllImport(Libraries.CORE_SYNCH_L1)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static unsafe  extern void LeaveCriticalSection(CRITICAL_SECTION* lpCriticalSection);

        [DllImport(Libraries.CORE_SYNCH_L1)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe void DeleteCriticalSection(CRITICAL_SECTION* lpCriticalSection);

    }
}
