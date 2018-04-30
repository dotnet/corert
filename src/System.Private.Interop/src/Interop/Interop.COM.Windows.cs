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
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace System.Runtime.InteropServices
{

    [CLSCompliant(false)]
    public static partial class ExternalInterop
    {
        private static partial class Libraries
        {
#if TARGET_CORE_API_SET
            internal const string CORE_COM = "api-ms-win-core-com-l1-1-0.dll";
#else
            internal const string CORE_COM = "ole32.dll";
#endif
            // @TODO: What is the matching dll in CoreSys?
            // @TODO: Replace the below by the correspondent api-ms-win-core-...-0.dll
            internal const string CORE_COM_AUT = "OleAut32.dll";
        }

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe int CoCreateInstanceFromApp(
            Guid* clsid,
            IntPtr pUnkOuter,
            int context,
            IntPtr reserved,
            int count,
            IntPtr results
        );

        [DllImport(Libraries.CORE_COM, PreserveSig = false)]
        internal static extern void CreateBindCtx(UInt32 reserved, out IBindCtx ppbc);

        [DllImport(Libraries.CORE_COM, PreserveSig = false)]
        internal static extern void MkParseDisplayName(IBindCtx pbc, [MarshalAs(UnmanagedType.LPWStr)] String szUserName, out UInt32 pchEaten, out IMoniker ppmk);

#if !TARGET_CORE_API_SET // BindMoniker not available in core API set
        [DllImport(Libraries.CORE_COM, PreserveSig = false)]
        internal static extern void BindMoniker(IMoniker pmk, UInt32 grfOpt, ref Guid iidResult, [MarshalAs(UnmanagedType.Interface)] out Object ppvResult);
#endif

        [DllImport(Libraries.CORE_COM_AUT)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern void VariantClear(IntPtr pObject);
        

        public static unsafe void SafeCoTaskMemFree(void* pv)
        {
            // Even though CoTaskMemFree is a no-op for NULLs, skipping the interop call entirely is faster
            if (pv != null)
                PInvokeMarshal.CoTaskMemFree(new IntPtr(pv));
        }
    }
}
