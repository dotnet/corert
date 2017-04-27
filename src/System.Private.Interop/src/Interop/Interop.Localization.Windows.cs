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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.InteropServices
{

    public static partial class ExternalInterop
    {
        private static partial class Libraries
        {
#if TARGET_CORE_API_SET
            internal const string CORE_LOCALIZATION = "api-ms-win-core-localization-l1-2-0.dll";
#else
            internal const string CORE_LOCALIZATION = "kernel32.dll";
#endif
        }
#if !CORECLR

        internal struct CPINFO
        {
#pragma warning disable 0649
            internal int MaxCharSize;

            // BYTE DefaultChar[MAX_DEFAULTCHAR];
            // I don't want to have MarshalAs in System.Private.Interop.dll
            internal byte DefaultChar0;
            internal byte DefaultChar1;
            internal byte DefaultChar2;
            internal byte DefaultChar3;
            internal byte DefaultChar4;
            internal byte DefaultChar5;
            internal byte DefaultChar6;
            internal byte DefaultChar7;
            internal byte DefaultChar8;
            internal byte DefaultChar9;
            internal byte DefaultChar10;
            internal byte DefaultChar11;

            // BYTE LeadByte[MAX_LEADBYTES]
            internal byte LeadByte0;
            internal byte LeadByte1;
#pragma warning restore 0649
        }

        [DllImport(Libraries.CORE_LOCALIZATION, EntryPoint = "FormatMessageW", SetLastError = true)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe int FormatMessage(
            int dwFlags,
            IntPtr lpSource,
            uint dwMessageId,
            uint dwLanguageId,
            char* lpBuffer,
            uint nSize,
            IntPtr Arguments);

        [DllImport(Libraries.CORE_LOCALIZATION)]
        [McgGeneratedNativeCallCodeAttribute]
        internal static extern unsafe int GetCPInfo(uint codePage, CPINFO* lpCpInfo);


        internal const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        internal const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        internal const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;
        internal const int FORMAT_MESSAGE_MAX_WIDTH_MASK = 0x000000FF;
        internal const int ERROR_INSUFFICIENT_BUFFER = 0x7A;
        //according to MSDN FormatMessage API doc, the buffer cannot be larger than 64K bytes.
        private const int MaxAllowedBufferSize = 64 * 1024;
        /// <summary>
        ///
        /// </summary>
        /// <param name="errorCode">HRESULT</param>
        /// <param name="sb">buffer</param>
        /// <param name="errorMsg">output error message</param>
        /// <returns>Return false IFF when buffer space isnt enough</returns>
        private static unsafe bool TryGetMessage(int errorCode, int bufferSize, out string errorMsg)
        {
            Debug.Assert(bufferSize > 0);
            errorMsg = null;
            char[] buffer = new char[bufferSize];
            int result;
            fixed (char* pinned_lpBuffer = &buffer[0])
            {
                result = ExternalInterop.FormatMessage(
                    FORMAT_MESSAGE_IGNORE_INSERTS | FORMAT_MESSAGE_FROM_SYSTEM |
                    FORMAT_MESSAGE_ARGUMENT_ARRAY | FORMAT_MESSAGE_MAX_WIDTH_MASK,
                    IntPtr.Zero, (uint)errorCode, 0, pinned_lpBuffer,
                    (uint)buffer.Length, IntPtr.Zero);
            }
            if (result != 0) //result hold the number of WCHARs stored in the output buffer(sb)
            {
                if (buffer[result - 1] == ' ')
                {
                    buffer[result - 1] = '\0';
                    result = result - 1;
                }
                errorMsg = new string(buffer, 0, result);
                return true;
            }
            else
            {
                if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER) return false;
                return true;
            }
        }

        /// <summary>
        /// Get Error Message according to HResult
        /// </summary>
        /// <param name="errorCode">HRESULT</param>
        /// <returns></returns>
        public static unsafe String GetMessage(int errorCode)
        {
            string errorMsg;
            int bufferSize = 1024;
            do
            {
                if (TryGetMessage(errorCode, bufferSize, out errorMsg))
                    return errorMsg;
                //Increase the size for buffer by 4
                bufferSize = bufferSize * 4;

            } while (bufferSize <= MaxAllowedBufferSize);

            return null;
        }
#endif //CORECLR
    }
}
