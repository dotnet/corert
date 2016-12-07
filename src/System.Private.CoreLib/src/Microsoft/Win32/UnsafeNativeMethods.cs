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

    internal static class UnsafeNativeMethods {

        [DllImport(Win32Native.KERNEL32, EntryPoint="GetTimeZoneInformation", SetLastError = true, ExactSpelling = true)]
        internal static extern int GetTimeZoneInformation(out Win32Native.TimeZoneInformation lpTimeZoneInformation);

        [DllImport(Win32Native.KERNEL32, EntryPoint="GetDynamicTimeZoneInformation", SetLastError = true, ExactSpelling = true)]
        internal static extern int GetDynamicTimeZoneInformation(out Win32Native.DynamicTimeZoneInformation lpDynamicTimeZoneInformation);

        // 
        // BOOL GetFileMUIPath(
        //   DWORD  dwFlags,
        //   PCWSTR  pcwszFilePath,
        //   PWSTR  pwszLanguage,
        //   PULONG  pcchLanguage,
        //   PWSTR  pwszFileMUIPath,
        //   PULONG  pcchFileMUIPath,
        //   PULONGLONG  pululEnumerator
        // );
        // 
        [DllImport(Win32Native.KERNEL32, EntryPoint="GetFileMUIPath", SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileMUIPath(
                                     int flags,
                                     [MarshalAs(UnmanagedType.LPWStr)]
                                     String filePath,
                                     [MarshalAs(UnmanagedType.LPWStr)]
                                     StringBuilder language,
                                     ref int languageLength,
                                     [MarshalAs(UnmanagedType.LPWStr)]
                                     StringBuilder fileMuiPath,
                                     ref int fileMuiPathLength,
                                     ref Int64 enumerator);   

        [DllImport(Win32Native.USER32, EntryPoint="LoadStringW",  SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true, CallingConvention=CallingConvention.StdCall)]
        internal static extern int LoadString(SafeLibraryHandle handle, int id, StringBuilder buffer, int bufferLength);

        [DllImport(Win32Native.KERNEL32, EntryPoint="LoadLibraryExW", CharSet=System.Runtime.InteropServices.CharSet.Unicode, SetLastError=true)]
        internal static extern SafeLibraryHandle LoadLibraryEx(string libFilename, IntPtr reserved, int flags);      
  
        [DllImport(Win32Native.KERNEL32, CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);       

        [DllImport(Win32Native.KERNEL32, CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int GetLastError();

#if FEATURE_COMINTEROP
        [DllImport("combase.dll", PreserveSig = true)]
        internal static extern int RoGetActivationFactory(
            [MarshalAs(UnmanagedType.HString)] string activatableClassId,
            [In] ref Guid iid,
            [Out,MarshalAs(UnmanagedType.IInspectable)] out Object factory);
#endif

    }
}
