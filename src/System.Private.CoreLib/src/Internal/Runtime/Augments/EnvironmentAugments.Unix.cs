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

            return Array.Empty<KeyValuePair<string,string>>();
        }
    }
}
