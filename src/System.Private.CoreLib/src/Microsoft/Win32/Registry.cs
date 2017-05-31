// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;

namespace Microsoft.Win32
{
    /**
     * Registry encapsulation. Contains members representing all top level system
     * keys.
     *
     * @security(checkClassLinking=on)
     */
    //This class contains only static members and does not need to be serializable.
    internal static class Registry
    {
        /**
         * Current User Key.
         * 
         * This key should be used as the root for all user specific settings.
         */
        public static readonly RegistryKey CurrentUser = RegistryKey.GetBaseKey(RegistryKey.HKEY_CURRENT_USER);

        /**
         * Local Machine Key.
         * 
         * This key should be used as the root for all machine specific settings.
         */
        public static readonly RegistryKey LocalMachine = RegistryKey.GetBaseKey(RegistryKey.HKEY_LOCAL_MACHINE);
    }
}

