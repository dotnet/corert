// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.DeveloperExperience;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Security;

namespace System.Diagnostics
{
    // .NET Native-specific Debug implementation
    public static partial class Debug
    {
        // internal and not read only so that the tests can swap this out.
        internal static IDebugLogger s_logger = new NetNativeDebugLogger();

        internal sealed class NetNativeDebugLogger : IDebugLogger
        {
            public void ShowAssertDialog(string stackTrace, string message, string detailMessage)
            {
                string fullMessage = message + Environment.NewLine + detailMessage;
                bool result = DeveloperExperience.Default.OnContractFailure(stackTrace, ContractFailureKind.Assert, fullMessage, null, null, null);
                if (!result)
                {
                    ExitProcess();
                }
            }

            public void WriteCore(string message)
            {
                // really huge messages mess up both VS and dbmon, so we chop it up into 
                // reasonable chunks if it's too big. This is the number of characters 
                // that OutputDebugstring chunks at.
                const int WriteChunkLength = 4091;

                // We don't want output from multiple threads to be interleaved.
                lock (s_ForLock)
                {
                    if (message == null || message.Length <= WriteChunkLength)
                    {
                        WriteToDebugger(message);
                    }
                    else
                    {
                        int offset;
                        for (offset = 0; offset < message.Length - WriteChunkLength; offset += WriteChunkLength)
                        {
                            WriteToDebugger(message.Substring(offset, WriteChunkLength));
                        }
                        WriteToDebugger(message.Substring(offset));
                    }
                }
            }

            private static void WriteToDebugger(string message)
            {
                Interop.mincore.OutputDebugString(message ?? string.Empty);
            }

            private static void ExitProcess()
            {
                Interop.mincore.ExitProcess((uint)(Interop.Constants.EFail));
            }
        }

        [DebuggerHidden]
        internal static void DebugBreak()
        {
            // IMPORTANT: This call will let us detect if  debug break is broken, and also
            // gives double chances.
            DebugBreak();
        }
    }
}
