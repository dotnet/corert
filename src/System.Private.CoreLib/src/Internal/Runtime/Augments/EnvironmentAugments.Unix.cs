// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        private static string GetEnvironmentVariableCore(string variable)
        {
            Debug.Assert(variable != null);
            return Marshal.PtrToStringAnsi(Interop.Sys.GetEnv(variable));
        }

        private static void SetEnvironmentVariableCore(string variable, string value)
        {
            Debug.Assert(variable != null);
            throw new NotImplementedException();
        }

        public static IEnumerable<KeyValuePair<string,string>> EnumerateEnvironmentVariables()
        {
            if ("".Length != 0)
                throw new NotImplementedException(); // Need to return something better than an empty environment block.

            unsafe
            {
                // Get byte** of environment variables from native interop
                var unsafeBlock = (byte**)Interop.Sys.EnvironGetSystemEnvironment();
                if (unsafeBlock == (byte**)0)
                    throw new OutOfMemoryException();
                
                // Find total length of two-dimensional byte**
                int rowIndex = 0;
                int totalLength = 0;
                while (unsafeBlock[rowIndex] != null && unsafeBlock[rowIndex][0] != 0)
                {
                    byte* p = unsafeBlock[rowIndex];
                    while (*p != 0) // Continue until end-of-line char '\0'
                    {
                        p++;
                    }
                    totalLength += checked((int)(p - unsafeBlock[rowIndex] + 1));
                    rowIndex++;
                }

                // Copy two-dimensional byte** to a flat char[] for parsing
                rowIndex = 0;
                var blockIndex = 0;
                char[] block = new char[totalLength];
                while (unsafeBlock[rowIndex] != null && unsafeBlock[rowIndex][0] != 0)
                {
                    byte* p = unsafeBlock[rowIndex];
                    while (*p != 0) // Continue until end-of-line char '\0'
                    {
                        p++;
                    }
                    var rowLength = checked((int)(p - unsafeBlock[rowIndex] + 1));
                    for (int i = 0; i < rowLength; i++)
                    {
                        // Copy original byte to a Unicode char in our flat char[]
                        block[blockIndex++] = Convert.ToChar(unsafeBlock[rowIndex][i]);
                    }
                    rowIndex++;
                }

                // Parse flat char[] and return
                return EnumerateEnvironmentVariables(block);
            }
        }

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

        private static void ExitRaw()
        {
            Interop.Sys.Exit(s_latchedExitCode);
        }
    }
}
