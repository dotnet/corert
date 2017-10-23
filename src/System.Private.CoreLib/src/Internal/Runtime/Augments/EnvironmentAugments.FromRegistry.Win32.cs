// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Win32;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        private static string GetEnvironmentVariableFromRegistry(string variable, bool fromMachine)
        {
            Debug.Assert(variable != null);
            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(fromMachine: fromMachine, writable: false))
            {
                return environmentKey?.GetValue(variable) as string;
            }
        }

        private static void SetEnvironmentVariableFromRegistry(string variable, string value, bool fromMachine)
        {
            Debug.Assert(variable != null);

            // User-wide environment variables stored in the registry are limited to 255 chars for the environment variable name.
            const int MaxUserEnvVariableLength = 255;
            if (variable.Length >= MaxUserEnvVariableLength)
                throw new ArgumentException(SR.Argument_LongEnvVarValue, nameof(variable));

            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(fromMachine: fromMachine, writable: true))
            {
                if (environmentKey != null)
                {
                    if (value == null)
                    {
                        environmentKey.DeleteValue(variable, throwOnMissingValue: false);
                    }
                    else
                    {
                        environmentKey.SetValue(variable, value);
                    }
                }
            }

            // send a WM_SETTINGCHANGE message to all windows
            IntPtr r = Interop.User32.SendMessageTimeout(new IntPtr(Interop.User32.HWND_BROADCAST), Interop.User32.WM_SETTINGCHANGE, IntPtr.Zero, "Environment", 0, 1000, IntPtr.Zero);
            if (r == IntPtr.Zero)
                Debug.Fail("SetEnvironmentVariable failed: " + Marshal.GetLastWin32Error());
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateEnvironmentVariablesFromRegistry(bool fromMachine)
        {
            using (RegistryKey environmentKey = OpenEnvironmentKeyIfExists(fromMachine: fromMachine, writable: false))
            {
                if (environmentKey != null)
                {
                    foreach (string name in environmentKey.GetValueNames())
                    {
                        string value = environmentKey.GetValue(name, string.Empty).ToString();
                        yield return new KeyValuePair<string, string>(name, value);
                    }
                }
            }
        }

        private static RegistryKey OpenEnvironmentKeyIfExists(bool fromMachine, bool writable)
        {
            RegistryKey baseKey;
            string keyName;

            if (fromMachine)
            {
                baseKey = Registry.LocalMachine;
                keyName = @"System\CurrentControlSet\Control\Session Manager\Environment";
            }
            else
            {
                baseKey = Registry.CurrentUser;
                keyName = "Environment";
            }

            return baseKey.OpenSubKey(keyName, writable: writable);
        }
    }
}
