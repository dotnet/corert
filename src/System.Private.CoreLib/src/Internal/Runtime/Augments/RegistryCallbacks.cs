// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>
    /// This helper class is used to provide registry support
    /// </summary>
    [CLSCompliant(false)]
    public abstract class RegistryCallbacks
    {
        /// <summary>
        /// Reads value names from a registry key under HKEY_LOCAL_MACHINE
        /// </summary>
        public abstract string[] GetHKLMValueNames(string key);

        /// <summary>
        /// Reads values from a registry key under HKEY_LOCAL_MACHINE
        /// </summary>
        public abstract string[] GetHKLMValues(string key);

        /// <summary>
        /// Reads a particular value from a registry key under HKEY_LOCAL_MACHINE
        /// </summary>
        public abstract string GetHKCUValue(string key, string value);
    }
}
