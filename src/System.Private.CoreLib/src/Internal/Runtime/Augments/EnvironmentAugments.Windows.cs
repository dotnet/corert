// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Win32Marshal = System.IO.Win32Marshal;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        private static string GetEnvironmentVariableCore(string variable)
        {
            Debug.Assert(variable != null);

            // The convention of the API is as follows:
            // You call the API with a buffer of a given size. 
            // If the size of the buffer is insufficient to hold the value, 
            //   the API will return the required size for the buffer. 
            // In that case we resize the buffer and try again.

            int currentSize = 128;
            for (;;)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(currentSize);

                int actualSize = Interop.mincore.GetEnvironmentVariable(variable, buffer, buffer.Length);
                if (actualSize <= buffer.Length)
                {
                    string result = (actualSize != 0) ? new string(buffer, 0, actualSize) : null;
                    ArrayPool<char>.Shared.Return(buffer);
                    return result;
                }

                ArrayPool<char>.Shared.Return(buffer);
                currentSize = actualSize;
            }
        }

        private static void SetEnvironmentVariableCore(string variable, string value)
        {
            Debug.Assert(variable != null);

            if (!Interop.Kernel32.SetEnvironmentVariable(variable, value))
            {
                int errorCode = Marshal.GetLastWin32Error();
                switch (errorCode)
                {
                    case Interop.Errors.ERROR_ENVVAR_NOT_FOUND:
                        // Allow user to try to clear a environment variable
                        return;
                    case Interop.Errors.ERROR_FILENAME_EXCED_RANGE:
                        // The error message from Win32 is "The filename or extension is too long",
                        // which is not accurate.
                        throw new ArgumentException(SR.Argument_LongEnvVarValue);
                    case Interop.Errors.ERROR_NOT_ENOUGH_MEMORY:
                    case Interop.Errors.ERROR_NO_SYSTEM_RESOURCES:
                        throw new OutOfMemoryException(Interop.Kernel32.GetMessage(errorCode));
                    default:
                        throw new ArgumentException(Interop.Kernel32.GetMessage(errorCode));
                }
            }
        }

        public static IEnumerable<KeyValuePair<string,string>> EnumerateEnvironmentVariables()
        {
            unsafe
            {
                char* unsafeBlock = Interop.Kernel32.GetEnvironmentStrings();
                if (unsafeBlock == (char*)0)
                    throw new OutOfMemoryException();
                try
                {
                    // Format for GetEnvironmentStrings is:
                    // [=HiddenVar=value\0]* [Variable=value\0]* \0
                    // See the description of Environment Blocks in MSDN's
                    // CreateProcess page (null-terminated array of null-terminated strings).

                    // Search for terminating \0\0 (two unicode \0's).
                    char* p = unsafeBlock;
                    while (!(*p == '\0' && *(p + 1) == '\0'))
                    {
                        p++;
                    }

                    int len = checked((int)(p - unsafeBlock + 1));
                    // TODO Perf: Change "block" from char[] to ReadOnlySpan<char> once that becomes available in Project N.
                    char[] block = new char[len];
                    for (int i = 0; i < len; i++)
                    {
                        block[i] = unsafeBlock[i];
                    }
                    return EnumerateEnvironmentVariables(block);
                }
                finally
                {
                    bool success = Interop.Kernel32.FreeEnvironmentStrings(unsafeBlock);
                    Debug.Assert(success);
                }
            }
        }

        // Format for GetEnvironmentStrings is:
        // (=HiddenVar=value\0 | Variable=value\0)* \0
        // See the description of Environment Blocks in MSDN's
        // CreateProcess page (null-terminated array of null-terminated strings).
        // Note the =HiddenVar's aren't always at the beginning.

        // Copy strings out, parsing into pairs.
        // The first few environment variable entries start with an '='.
        // The current working directory of every drive (except for those drives
        // you haven't cd'ed into in your DOS window) are stored in the 
        // environment block (as =C:=pwd) and the program's exit code is 
        // as well (=ExitCode=00000000).
        private static IEnumerable<KeyValuePair<string, string>> EnumerateEnvironmentVariables(char[] block)
        {
            // To maintain complete compatibility with prior versions we need to return a Hashtable.
            // We did ship a prior version of Core with LowLevelDictionary, which does iterate the
            // same (e.g. yields DictionaryEntry), but it is not a public type.
            //
            // While we could pass Hashtable back from CoreCLR the type is also defined here. We only
            // want to surface the local Hashtable.
            for (int i = 0; i < block.Length; i++)
            {
                int startKey = i;

                // Skip to key. On some old OS, the environment block can be corrupted.
                // Some will not have '=', so we need to check for '\0'. 
                while (block[i] != '=' && block[i] != '\0')
                    i++;
                if (block[i] == '\0')
                    continue;

                // Skip over environment variables starting with '='
                if (i - startKey == 0)
                {
                    while (block[i] != 0)
                        i++;
                    continue;
                }

                string key = new string(block, startKey, i - startKey);
                i++;  // skip over '='

                int startValue = i;
                while (block[i] != 0)
                    i++; // Read to end of this entry 
                string value = new string(block, startValue, i - startValue); // skip over 0 handled by for loop's i++

                yield return new KeyValuePair<string, string>(key, value);
            }
        }
    }
}
