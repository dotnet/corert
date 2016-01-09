// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security;
using Internal.NativeFormat;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Runtime;

namespace Internal.Runtime.CompilerHelpers
{
    internal static class StartupCodeHelpers
    {
        internal static void Initialize()
        {
            InitializeStringTable();
            RuntimeImports.RhEnableShutdownFinalization(0xffffffffu);
        }

        internal static void Shutdown()
        {
        }

        internal static unsafe void InitializeCommandLineArgsW(int argc, char** argv)
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; ++i)
            {
                args[i] = new string(argv[i]);
            }
            Environment.SetCommandLineArgs(args);
        }

        internal static unsafe void InitializeCommandLineArgs(int argc, byte** argv)
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; ++i)
            {
                byte* argval = argv[i];
                int len = CStrLen(argval);
                args[i] = Encoding.UTF8.GetString(argval, len);
            }
            Environment.SetCommandLineArgs(args);
        }

        private static unsafe void InitializeStringTable()
        {
            int length = 0;
            IntPtr strEETypePtr = GetModuleSection((int)ModuleSectionIds.StringEETypePtr, out length);
            Contract.Assert(length == IntPtr.Size);

            IntPtr strTableStart = GetModuleSection((int)ModuleSectionIds.StringFixupStart, out length);
            Contract.Assert(length % IntPtr.Size == 0);

            IntPtr strTableEnd = (IntPtr)((byte*)strTableStart + length);

            for (IntPtr* tab = (IntPtr*)strTableStart; tab < (IntPtr*)strTableEnd; tab++)
            {
                byte* bytes = (byte*)*tab;
                int len = (int)NativePrimitiveDecoder.DecodeUnsigned(ref bytes);
                int count = LowLevelUTF8Encoding.GetCharCount(bytes, len);
                Contract.Assert(count >= 0);

                string newStr = RuntimeImports.RhNewArrayAsString(new EETypePtr(strEETypePtr), count);
                fixed (char* dest = newStr)
                {
                    int newCount = LowLevelUTF8Encoding.GetChars(bytes, len, dest, count);
                    Contract.Assert(newCount == count);
                }
                GCHandle handle = GCHandle.Alloc(newStr);
                *tab = (IntPtr)handle;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int CStrLen(byte* str)
        {
            int len = 0;
            for (; str[len] != 0; len++) { }
            return len;
        }


        internal enum ModuleSectionIds
        {
            StringEETypePtr,
            StringFixupStart
        };

        [RuntimeImport(".", "GetModuleSection")]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [SecurityCritical] // required to match contract
        private static extern IntPtr GetModuleSection(int id, out int length);
    }
}
