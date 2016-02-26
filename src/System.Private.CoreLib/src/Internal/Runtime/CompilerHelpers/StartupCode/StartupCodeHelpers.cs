// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Security;
using Internal.NativeFormat;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Runtime;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.CompilerHelpers
{
    [McgIntrinsics]
    internal static class StartupCodeHelpers
    {
        internal static void Initialize()
        {
            InitializeStringTable();
            RunEagerClassConstructors();
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

        private static void Call(System.IntPtr pfn)
        {
        }

        private static unsafe void RunEagerClassConstructors()
        {
            int length = 0;
            IntPtr cctorTableStart = GetModuleSection((int)ModuleSectionIds.EagerCctorStart, out length);
            Debug.Assert(length % IntPtr.Size == 0);

            IntPtr cctorTableEnd = (IntPtr)((byte*)cctorTableStart + length);

            for (IntPtr* tab = (IntPtr*)cctorTableStart; tab < (IntPtr*)cctorTableEnd; tab++)
            {
                Call(*tab);
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
            StringFixupStart,
            EagerCctorStart,
        };

        [RuntimeImport(".", "GetModuleSection")]
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [SecurityCritical] // required to match contract
        private static extern IntPtr GetModuleSection(int id, out int length);
    }
}
