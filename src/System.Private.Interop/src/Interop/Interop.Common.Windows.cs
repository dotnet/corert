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


// TODO : Split this file , now it contains anything other than string and memoryreleated.
namespace System.Runtime.InteropServices
{

    public static partial class ExternalInterop
    {
        private static partial class Libraries
        {
#if TARGET_CORE_API_SET
            internal const string CORE_DEBUG = "api-ms-win-core-debug-l1-1-0.dll";
#else
        internal const string CORE_DEBUG = "kernel32.dll";
#endif //TARGET_CORE_API_SET
        }

        [DllImport(Libraries.CORE_DEBUG, EntryPoint = "OutputDebugStringW")]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe void OutputDebugString(char* lpOutputString);

        internal static unsafe void OutputDebugString(string outputString)
        {
            fixed (char* pOutputString = outputString)
            {
                OutputDebugString(pOutputString);
            }
        }
    }
}
