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
            IntPtr block = Interop.Sys.GetEnviron();
            if (block == IntPtr.Zero)
                yield break;

            // Per man page, environment variables come back as an array of pointers to strings
            // Parse each pointer of strings individually
            while (ParseEntry(block, out string key, out string value))
            {
                if (key != null && value != null)
                    yield return new KeyValuePair<string, string>(key, value);

                // Increment to next environment variable entry
                block += IntPtr.Size;
            }

            // Use a local, unsafe function since we cannot use `yield return` inside of an `unsafe` block
            unsafe bool ParseEntry(IntPtr current, out string key, out string value)
            {
                // Setup
                key = null; 
                value = null;

                // Point to current entry
                byte* entry = *(byte**)current;

                // Per man page, "The last pointer in this array has the value NULL"
                // Therefore, if entry is null then we're at the end and can bail
                if (entry == null)
                    return false;

                // Parse each byte of the entry until we hit either the separator '=' or '\0'.
                // This finds the split point for creating key/value strings below.
                // On some old OS, the environment block can be corrupted.
                // Some will not have '=', so we need to check for '\0'. 
                byte* splitpoint = entry;
                while (*splitpoint != '=' && *splitpoint != '\0')
                    splitpoint++;

                // Skip over entries starting with '=' and entries with no value (just a null-terminating char '\0')
                if (splitpoint == entry || *splitpoint == '\0')
                    return true;

                // The key is the bytes from start (0) until our splitpoint
                key = new string((sbyte*)entry, 0, checked((int)(splitpoint - entry)));
                // The value is the rest of the bytes starting after the splitpoint
                value = new string((sbyte*)(splitpoint + 1));

                return true;
            }
        }

        private static void ExitRaw()
        {
            Interop.Sys.Exit(s_latchedExitCode);
        }
    }
}
