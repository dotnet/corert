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
using System.Runtime.InteropServices;

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

#if CORECLR

        public static unsafe void* CoTaskMemAlloc(IntPtr size)
        {
            return Marshal.AllocHGlobal(size).ToPointer();
        }

        public static unsafe void CoTaskMemFree(void* pv)
        {
            Marshal.FreeHGlobal(new IntPtr(pv));
        }

        public static unsafe IntPtr SysAllocStringLen(char* pStrIn, UInt32 dwSize)
        {
            string srcString = new string(pStrIn, 0, checked((int)dwSize));
            return Marshal.StringToBSTR(srcString);
        }
        
        public static unsafe void SysFreeString(void* pBSTR)
        {
          SysFreeString(new IntPtr(pBSTR));
        }

        public static unsafe void SysFreeString(IntPtr pBSTR)
        {
            Marshal.FreeBSTR(pBSTR);
        }

        static internal void VariantClear(IntPtr pObject)
        {
            //Nop
        }

        static internal unsafe int CoGetMarshalSizeMax(out ulong pulSize, ref Guid iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags)
        {
            throw new PlatformNotSupportedException("CoGetMarshalSizeMax");
        }       

        static internal unsafe int CoGetObjectContext(ref Guid iid, out IntPtr ppv)
        {
            throw new PlatformNotSupportedException("CoGetObjectContext");
        }
               
        static internal unsafe int CoMarshalInterface(IntPtr pStream, ref Guid iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags)
        {
            throw new PlatformNotSupportedException("CoMarshalInterface");
        }

        static internal unsafe int CoUnmarshalInterface(IntPtr pStream, ref Guid iid, out IntPtr ppv)
        {
            throw new PlatformNotSupportedException("CoUnmarshalInterface");
        }
        
        static internal int CoReleaseMarshalData(IntPtr pStream)
        {
            // Nop in CoreCLR
            return 0;
        }

#else 
        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        public static extern unsafe void* CoTaskMemAlloc(IntPtr size);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        public extern static unsafe void CoTaskMemFree(void* pv);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        public static extern unsafe int CoGetContextToken(IntPtr* ppToken);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        static internal extern IntPtr CoTaskMemRealloc(IntPtr pv, IntPtr size);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        static internal unsafe extern int CoGetObjectContext(Guid* iid, void* ppv);


        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        static internal unsafe extern int CoCreateInstanceFromApp(
            Guid* clsid,
            IntPtr pUnkOuter,
            int context,
            IntPtr reserved,
            int count,
            IntPtr results
        );



        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        static internal unsafe extern int CoCreateFreeThreadedMarshaler(void* pOuter, void** ppunkMarshal);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        private extern unsafe static int CoMarshalInterface(IntPtr pStream, Guid* iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        private static unsafe extern int CoUnmarshalInterface(IntPtr pStream, Guid* iid, void** ppv);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        private static unsafe extern int CoGetMarshalSizeMax(ulong* pulSize, Guid* iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        static internal extern int CoReleaseMarshalData(IntPtr pStream);


        [DllImport(Libraries.CORE_COM_AUT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe void SysFreeString(void* pBSTR);

        public static unsafe void SysFreeString(IntPtr pBstr)
        {
            SysFreeString((void*)pBstr);
        }

        [DllImport(Libraries.CORE_COM_AUT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe uint SysStringLen(void* pBSTR);
        public static unsafe uint SysStringLen(IntPtr pBSTR)
        {
            return SysStringLen((void*)pBSTR);
        }

        [DllImport(Libraries.CORE_COM_AUT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe IntPtr SysAllocString(IntPtr pStrIn);

        [DllImport(Libraries.CORE_COM_AUT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe char* SysAllocStringLen(char* pStrIn, uint len);

        [DllImport(Libraries.CORE_COM_AUT)]
        [McgGeneratedNativeCallCodeAttribute]
        static internal extern void VariantClear(IntPtr pObject);

        static internal unsafe int CoGetObjectContext(ref Guid iid, out IntPtr ppv)
        {
            fixed (void* unsafe_ppv = &ppv)
            {
                fixed (Guid* unsafe_iid = &iid)
                {
                    return CoGetObjectContext(unsafe_iid, (void**)unsafe_ppv);
                }
            }
        }

        /// <summary>
        /// Marshal IUnknown * into IStream*
        /// </summary>
        /// <returns>HResult</returns>
        static internal unsafe int CoMarshalInterface(IntPtr pStream, ref Guid iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags)
        {
            fixed (Guid* unsafe_iid = &iid)
            {
                return CoMarshalInterface(pStream, unsafe_iid, pUnk, dwDestContext, pvDestContext, mshlflags);
            }
        }

        /// <summary>
        /// Marshal IStream* into IUnknown*
        /// </summary>
        /// <returns>HResult</returns>
        static internal unsafe int CoUnmarshalInterface(IntPtr pStream, ref Guid iid, out IntPtr ppv)
        {
            fixed (Guid* unsafe_iid = &iid)
            {
                fixed (void* unsafe_ppv = &ppv)
                {
                    return CoUnmarshalInterface(pStream, unsafe_iid, (void**)unsafe_ppv);
                }
            }
        }

        /// <summary>
        /// Returns an upper bound on the number of bytes needed to marshal the specified interface pointer to the specified object.
        /// </summary>
        /// <returns>HResult</returns>
        static internal unsafe int CoGetMarshalSizeMax(out ulong pulSize, ref Guid iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags)
        {
            fixed (ulong* unsafe_pulSize = &pulSize)
            {
                fixed (Guid* unsafe_iid = &iid)
                {
                    return CoGetMarshalSizeMax(unsafe_pulSize, unsafe_iid, pUnk, dwDestContext, pvDestContext, mshlflags);
                }
            }
        }

        public static unsafe int CoGetContextToken(out IntPtr ppToken)
        {
            ppToken = IntPtr.Zero;
            fixed (IntPtr* unsafePpToken = &ppToken)
            {
                return CoGetContextToken(unsafePpToken);
            }
        }

        public static unsafe void SafeCoTaskMemFree(void* pv)
        {
            // Even though CoTaskMemFree is a no-op for NULLs, skipping the interop call entirely is faster
            if (pv != null)
                CoTaskMemFree(pv);
        }
#endif //CORECLR
    }
}
