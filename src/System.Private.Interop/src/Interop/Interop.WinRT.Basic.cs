// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{
#if ENABLE_MIN_WINRT
    public static partial class McgMarshal
    {
        /// <summary>
        /// Creates a temporary HSTRING on the staack
        /// NOTE: pchPinnedSourceString must be pinned before calling this function, making sure the pointer
        /// is valid during the entire interop call
        /// </summary>
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe void StringToHStringReference(
            char* pchPinnedSourceString,
            string sourceString,
            HSTRING_HEADER* pHeader,
            HSTRING* phString)
        {
            if (sourceString == null)
                throw new ArgumentNullException(nameof(sourceString), SR.Null_HString);

            int hr = ExternalInterop.WindowsCreateStringReference(
                pchPinnedSourceString,
                (uint)sourceString.Length,
                pHeader,
                (void**)phString);

            if (hr < 0)
                throw Marshal.GetExceptionForHR(hr);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static unsafe string HStringToString(IntPtr hString)
        {
            HSTRING hstring = new HSTRING(hString);
            return HStringToString(hstring);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe string HStringToString(HSTRING pHString)
        {
            if (pHString.handle == IntPtr.Zero)
            {
                return String.Empty;
            }

            uint length = 0;
            char* pchBuffer = ExternalInterop.WindowsGetStringRawBuffer(pHString.handle.ToPointer(), &length);

            return new string(pchBuffer, 0, (int)length);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static unsafe void FreeHString(IntPtr pHString)
        {
            ExternalInterop.WindowsDeleteString(pHString.ToPointer());
        }
    }
#endif 

    public static partial class ExternalInterop
    {
#if ENABLE_MIN_WINRT
        [DllImport(Libraries.CORE_WINRT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe int RoGetActivationFactory(void* hstring_typeName, Guid* iid, void* ppv);


        [DllImport(Libraries.CORE_WINRT_STRING)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static extern unsafe int WindowsCreateStringReference(char* sourceString,
                                                                       uint length,
                                                                       HSTRING_HEADER* phstringHeader,
                                                                       void* hstring);
#endif

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe int CoCreateFreeThreadedMarshaler(void* pOuter, void** ppunkMarshal);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        public static extern unsafe int CoGetContextToken(IntPtr* ppToken);


        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe int CoGetObjectContext(Guid* iid, void* ppv);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        private static extern unsafe int CoGetMarshalSizeMax(ulong* pulSize, Guid* iid, IntPtr pUnk, 
                                                             Interop.COM.MSHCTX dwDestContext, 
                                                             IntPtr pvDestContext, 
                                                             Interop.COM.MSHLFLAGS mshlflags);
        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        private extern static unsafe int CoMarshalInterface(IntPtr pStream, Guid* iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags);

        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        private static extern unsafe int CoUnmarshalInterface(IntPtr pStream, Guid* iid, void** ppv);


        [DllImport(Libraries.CORE_COM)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        internal static extern int CoReleaseMarshalData(IntPtr pStream);


        /// <summary>
        /// Marshal IUnknown * into IStream*
        /// </summary>
        /// <returns>HResult</returns>
        internal static unsafe int CoMarshalInterface(IntPtr pStream, ref Guid iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags)
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
        internal static unsafe int CoUnmarshalInterface(IntPtr pStream, ref Guid iid, out IntPtr ppv)
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
        internal static unsafe int CoGetMarshalSizeMax(out ulong pulSize, ref Guid iid, IntPtr pUnk, Interop.COM.MSHCTX dwDestContext, IntPtr pvDestContext, Interop.COM.MSHLFLAGS mshlflags)
        {
            fixed (ulong* unsafe_pulSize = &pulSize)
            {
                fixed (Guid* unsafe_iid = &iid)
                {
                    return CoGetMarshalSizeMax(unsafe_pulSize, unsafe_iid, pUnk, dwDestContext, pvDestContext, mshlflags);
                }
            }
        }

#if ENABLE_MIN_WINRT
        internal static unsafe void RoGetActivationFactory(string className, ref Guid iid, out IntPtr ppv)
        {
            fixed (char* unsafe_className = className)
            {
                void* hstring_typeName = null;

                HSTRING_HEADER hstringHeader;
                int hr =
                    WindowsCreateStringReference(
                        unsafe_className, (uint)className.Length, &hstringHeader, &hstring_typeName);

                if (hr < 0)
                    throw Marshal.GetExceptionForHR(hr);

                fixed (Guid* unsafe_iid = &iid)
                {
                    fixed (void* unsafe_ppv = &ppv)
                    {
                        hr = ExternalInterop.RoGetActivationFactory(
                            hstring_typeName,
                            unsafe_iid,
                            unsafe_ppv);

                        if (hr < 0)
                            throw Marshal.GetExceptionForHR(hr);
                    }
                }
            }
        }
#endif

        public static unsafe int CoGetContextToken(out IntPtr ppToken)
        {
            ppToken = IntPtr.Zero;
            fixed (IntPtr* unsafePpToken = &ppToken)
            {
                return CoGetContextToken(unsafePpToken);
            }
        }

        internal static unsafe int CoGetObjectContext(ref Guid iid, out IntPtr ppv)
        {
            fixed (void* unsafe_ppv = &ppv)
            {
                fixed (Guid* unsafe_iid = &iid)
                {
                    return CoGetObjectContext(unsafe_iid, (void**)unsafe_ppv);
                }
            }
        }
    }
}
