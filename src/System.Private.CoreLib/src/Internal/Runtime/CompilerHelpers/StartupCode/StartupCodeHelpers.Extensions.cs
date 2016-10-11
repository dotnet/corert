// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Internal.NativeFormat;
using Internal.Runtime.Augments;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    partial class StartupCodeHelpers
    {
        internal static unsafe void InitializeCommandLineArgsW(int argc, char** argv)
        {
            string[] args = new string[argc];
            for (int i = 0; i < argc; ++i)
            {
                args[i] = new string(argv[i]);
            }
            EnvironmentAugments.SetCommandLineArgs(args);
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
            EnvironmentAugments.SetCommandLineArgs(args);
        }

        private static string[] GetMainMethodArguments()
        {
            // GetCommandLineArgs includes the executable name, Main() arguments do not.
            string[] args = EnvironmentAugments.GetCommandLineArgs();

            Debug.Assert(args.Length > 0);

            string[] mainArgs = new string[args.Length - 1];
            Array.Copy(args, 1, mainArgs, 0, mainArgs.Length);

            return mainArgs;
        }

        static unsafe partial void InitializeStringTable(IntPtr stringTableStart, int length)
        {
            IntPtr stringTableEnd = (IntPtr)((byte*)stringTableStart + length);
            for (IntPtr* tab = (IntPtr*)stringTableStart; tab < (IntPtr*)stringTableEnd; tab++)
            {
                byte* bytes = (byte*)*tab;
                int len = (int)NativePrimitiveDecoder.DecodeUnsigned(ref bytes);
                int count = LowLevelUTF8Encoding.GetCharCount(bytes, len);
                Debug.Assert(count >= 0);

                string newStr = RuntimeImports.RhNewArrayAsString(EETypePtr.EETypePtrOf<string>(), count);
                fixed (char* dest = newStr)
                {
                    int newCount = LowLevelUTF8Encoding.GetChars(bytes, len, dest, count);
                    Debug.Assert(newCount == count);
                }
                GCHandle handle = GCHandle.Alloc(newStr);
                *tab = (IntPtr)handle;
            }
        }
    }
}
