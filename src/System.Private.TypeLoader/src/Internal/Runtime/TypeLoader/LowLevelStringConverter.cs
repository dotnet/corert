// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Text;
using Internal.Runtime.Augments;

namespace Internal.Runtime.TypeLoader
{
    /// <summary>
    /// Extension methods that provide low level ToString() equivalents for some of the core types.
    /// Calling regular ToString() on these types goes through a lot of the CultureInfo machinery
    /// which is not low level enough for the type loader purposes.
    /// </summary>
    internal static class LowLevelStringConverter
    {
        private const string HexDigits = "0123456789ABCDEF";

        public static string LowLevelToString(this int arg)
        {
            return ((uint)arg).LowLevelToString();
        }

        public static string LowLevelToString(this uint arg)
        {
            StringBuilder sb = new StringBuilder(8);
            int shift = 4 * 8;
            while (shift > 0)
            {
                shift -= 4;
                int digit = (int)((arg >> shift) & 0xF);
                sb.Append(HexDigits[digit]);
            }

            return sb.ToString();
        }

        public static string LowLevelToString(this IntPtr arg)
        {
            StringBuilder sb = new StringBuilder(IntPtr.Size * 4);
            ulong num = (ulong)arg;

            int shift = IntPtr.Size * 8;
            while (shift > 0)
            {
                shift -= 4;
                int digit = (int)((num >> shift) & 0xF);
                sb.Append(HexDigits[digit]);
            }

            return sb.ToString();
        }

        public static string LowLevelToString(this RuntimeTypeHandle rtth)
        {
            // If reflection callbacks are already initialized, we can use that to get a metadata name.
            // Otherwise it's too early and the best we can do is to return the pointer.
            if (RuntimeAugments.CallbacksIfAvailable != null)
            {
                string name;
                if (RuntimeAugments.Callbacks.TryGetMetadataNameForRuntimeTypeHandle(rtth, out name))
                    return name;
            }

            string prefix = "EEType:0x";

            StringBuilder sb = new StringBuilder(prefix.Length + IntPtr.Size * 4);
            ulong num = (ulong)rtth.ToIntPtr();

            int shift = IntPtr.Size * 8;
            while (shift > 0)
            {
                shift -= 4;
                int digit = (int)((num >> shift) & 0xF);
                sb.Append(HexDigits[digit]);
            }

            return sb.ToString();
        }
    }
}
