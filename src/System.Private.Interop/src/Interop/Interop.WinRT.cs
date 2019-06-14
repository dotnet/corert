// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{

    public static partial class ExternalInterop
    {
        private static partial class Libraries
        {
            internal const string CORE_WINRT = "api-ms-win-core-winrt-l1-1-0.dll";
            internal const string CORE_WINRT_STRING = "api-ms-win-core-winrt-string-l1-1-0.dll";
            internal const string CORE_WINRT_ERROR1 = "api-ms-win-core-winrt-error-l1-1-1.dll";
            internal const string CORE_WINRT_ERROR = "api-ms-win-core-winrt-error-l1-1-0.dll";
            internal const string CORE_WINRT_TYPERESOLUTION = "api-ms-win-ro-typeresolution-l1-1-0.dll";
        }

        [DllImport(Libraries.CORE_WINRT_STRING)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe int WindowsCreateString(char* sourceString,
                                                            uint length,
                                                            void* hstring);




        [DllImport(Libraries.CORE_WINRT_STRING)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe void WindowsDeleteString(void* hstring);
        

        [DllImport(Libraries.CORE_WINRT_STRING)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe char* WindowsGetStringRawBuffer(void* hstring, uint* pLength);

        [DllImport(Libraries.CORE_WINRT)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern unsafe int RoActivateInstance(void* hActivatableClassId, out void* ppv);


        [DllImport(Libraries.CORE_WINRT_ERROR, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        public static extern int GetRestrictedErrorInfo(out System.IntPtr pRestrictedErrorInfo);

        [DllImport(Libraries.CORE_WINRT_ERROR, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern int RoOriginateError(int hr, HSTRING hstring);

        [DllImport(Libraries.CORE_WINRT_ERROR, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static extern int SetRestrictedErrorInfo(System.IntPtr pRestrictedErrorInfo);

        [DllImport(Libraries.CORE_WINRT_ERROR1, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern int RoOriginateLanguageException(int hr, HSTRING message, IntPtr pLanguageException);

        [DllImport(Libraries.CORE_WINRT_ERROR1, PreserveSig = true)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern int RoReportUnhandledError(IntPtr pRestrictedErrorInfo);

        [DllImport(Libraries.CORE_WINRT_TYPERESOLUTION, PreserveSig = true)]
        internal static unsafe extern int RoParseTypeName(
            HSTRING typename,
            uint * typenamePartsLength,
            IntPtr ** typenameParts);
    }
}
