// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.DeveloperExperience;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Security;

namespace System.Diagnostics
{
    public static partial class Debug
    {
        private static void ShowDialog(string stackTrace, string message, string detailMessage, string errorSource)
        {
            // We can safely ignore errorSource since it's a CoreCLR specific argument for distinguishing calls from Debug.Assert and Environment.FailFast
            string fullMessage = message + Environment.NewLine + detailMessage;
            bool result = DeveloperExperience.Default.OnContractFailure(stackTrace, ContractFailureKind.Assert, fullMessage, null, null, null);
            if (!result)
            {
                RuntimeExceptionHelpers.FailFast(fullMessage);
            }
        }

        private static readonly object s_ForLock = new object();

        private static void WriteCore(string message)
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
    }
}
