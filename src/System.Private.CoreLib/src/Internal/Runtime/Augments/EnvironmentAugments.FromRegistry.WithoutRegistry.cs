// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        private static string GetEnvironmentVariableFromRegistry(string variable, bool fromMachine)
        {
            Debug.Assert(variable != null);
            return null; // Systems without registries pretend that the registry environment subkeys are empty lists.
        }

        private static void SetEnvironmentVariableFromRegistry(string variable, string value, bool fromMachine)
        {
            Debug.Assert(variable != null);
            return;  // Systems without registries pretend that the registry environment subkeys are empty lists that throw all write requests into a black hole.
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateEnvironmentVariablesFromRegistry(bool fromMachine)
        {
            return Array.Empty<KeyValuePair<string, string>>();  // Systems without registries pretend that the registry environment subkeys are empty lists.
        }
    }
}
