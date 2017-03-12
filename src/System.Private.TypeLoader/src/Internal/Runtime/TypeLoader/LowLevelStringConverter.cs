// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Text;

using System.Reflection.Runtime.General;

using Internal.Metadata.NativeFormat;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;

namespace System
{
    internal static class TypeLoaderFormattingHelpers
    {
        public static string ToStringInvariant(this int arg)
        {
            return arg.LowLevelToString();
        }
    }
}

namespace Internal.Runtime.TypeLoader
{
    /// <summary>
    /// Extension methods that provide low level ToString() equivalents for some of the core types.
    /// Calling regular ToString() on these types goes through a lot of the CultureInfo machinery
    /// which is not low level enough for the type loader purposes.
    /// </summary>
    internal static partial class LowLevelStringConverter
    {
        private const string HexDigits = "0123456789ABCDEF";

        public static string LowLevelToString(this int arg)
        {
            return ((uint)arg).LowLevelToString();
        }

        public static string LowLevelToString(this LayoutInt arg)
        {
            if (arg.IsIndeterminate)
                return "Indeterminate";
            else
                return ((uint)arg.AsInt).LowLevelToString();
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
            TypeReferenceHandle typeRefHandle;
            QTypeDefinition qTypeDefinition;
            MetadataReader reader;

            // Try to get the name from metadata
            if (TypeLoaderEnvironment.Instance.TryGetMetadataForNamedType(rtth, out qTypeDefinition))
            {
#if ECMA_METADATA_SUPPORT
                string result = EcmaMetadataFullName(qTypeDefinition);
                if (result != null)
                    return result;
#endif

                reader = qTypeDefinition.NativeFormatReader;
                TypeDefinitionHandle typeDefHandle = qTypeDefinition.NativeFormatHandle;
                return typeDefHandle.GetFullName(reader);
            }

            // Try to get the name from diagnostic metadata
            if (TypeLoaderEnvironment.TryGetTypeReferenceForNamedType(rtth, out reader, out typeRefHandle))
            {
                return typeRefHandle.GetFullName(reader);
            }

            // Fallback implementation when no metadata available
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
