// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  AssemblyNameHelpers
**
==============================================================*/

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace System.Reflection.Runtime.Assemblies
{
    internal static partial class AssemblyNameHelpers
    {
        internal static String ComputeDisplayName(RuntimeAssemblyName a)
        {
            if (a.Name == String.Empty)
                throw new FileLoadException();

            StringBuilder sb = new StringBuilder();
            if (a.Name != null)
            {
                sb.AppendQuoted(a.Name);
            }

            if (a.Version != null)
            {
                sb.Append(", Version=");
                sb.Append(a.Version.ToString());
            }

            String cultureName = a.CultureName;
            if (cultureName != null)
            {
                if (cultureName == String.Empty)
                    cultureName = "neutral";
                sb.Append(", Culture=");
                sb.AppendQuoted(cultureName);
            }

            byte[] pkt = a.PublicKeyOrToken;
            if (pkt != null)
            {
                if (0 != (a.Flags & AssemblyNameFlags.PublicKey))
                    pkt = ComputePublicKeyToken(pkt);

                if (pkt.Length > PUBLIC_KEY_TOKEN_LEN)
                    throw new ArgumentException();

                sb.Append(", PublicKeyToken=");
                if (pkt.Length == 0)
                    sb.Append("null");
                else
                {
                    foreach (byte b in pkt)
                    {
                        sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    }
                }
            }

            if (0 != (a.Flags & AssemblyNameFlags.Retargetable))
                sb.Append(", Retargetable=Yes");

            AssemblyContentType contentType = ExtractAssemblyContentType(a.Flags);
            if (contentType == AssemblyContentType.WindowsRuntime)
                sb.Append(", ContentType=WindowsRuntime");

            // NOTE: By design (desktop compat) AssemblyName.FullName and ToString() do not include ProcessorArchitecture.

            return sb.ToString();
        }

        private static void AppendQuoted(this StringBuilder sb, String s)
        {
            bool needsQuoting = false;
            const char quoteChar = '\"';

            //@todo: App-compat: You can use double or single quotes to quote a name, and Fusion (or rather the IdentityAuthority) picks one
            // by some algorithm. Rather than guess at it, I'll just use double-quote consistently.
            if (s != s.Trim() || s.Contains("\"") || s.Contains("\'"))
                needsQuoting = true;

            if (needsQuoting)
                sb.Append(quoteChar);

            for (int i = 0; i < s.Length; i++)
            {
                bool addedEscape = false;
                foreach (KeyValuePair<char, String> kv in AssemblyNameLexer.EscapeSequences)
                {
                    String escapeReplacement = kv.Value;
                    if (!(s[i] == escapeReplacement[0]))
                        continue;
                    if ((s.Length - i) < escapeReplacement.Length)
                        continue;
                    String prefix = s.Substring(i, escapeReplacement.Length);
                    if (prefix == escapeReplacement)
                    {
                        sb.Append('\\');
                        sb.Append(kv.Key);
                        addedEscape = true;
                    }
                }

                if (!addedEscape)
                    sb.Append(s[i]);
            }

            if (needsQuoting)
                sb.Append(quoteChar);
        }


        //
        // Converts an AssemblyName to a RuntimeAssemblyName that is free from any future mutations on the AssemblyName.
        //
        internal static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyName assemblyName)
        {
            if (assemblyName.Name == null)
                throw new ArgumentException();

            AssemblyNameFlags flags = assemblyName.Flags;
            AssemblyContentType contentType = assemblyName.ContentType;
            ProcessorArchitecture processorArchitecture = assemblyName.ProcessorArchitecture;
            AssemblyNameFlags combinedFlags = CombineAssemblyNameFlags(flags, contentType, processorArchitecture);
            byte[] pkOriginal;
            if (0 != (flags & AssemblyNameFlags.PublicKey))
                pkOriginal = assemblyName.GetPublicKey();
            else
                pkOriginal = assemblyName.GetPublicKeyToken();

            // AssemblyName's PKT property getters do NOT copy the array before giving it out. Make our own copy
            // as the original is wide open to tampering by anyone.
            byte[] pkCopy = null;
            if (pkOriginal != null)
            {
                pkCopy = new byte[pkOriginal.Length];
                ((ICollection<byte>)pkOriginal).CopyTo(pkCopy, 0);
            }

            return new RuntimeAssemblyName(assemblyName.Name, assemblyName.Version, assemblyName.CultureName, combinedFlags, pkCopy);
        }

        //
        // These helpers convert between the combined flags+contentType+processorArchitecture value and the separated parts.
        //
        // Since these are only for trusted callers, they do NOT check for out of bound bits. 
        //

        internal static AssemblyContentType ExtractAssemblyContentType(AssemblyNameFlags flags)
        {
            return (AssemblyContentType)((((int)flags) >> 9) & 0x7);
        }

        internal static ProcessorArchitecture ExtractProcessorArchitecture(AssemblyNameFlags flags)
        {
            return (ProcessorArchitecture)((((int)flags) >> 4) & 0x7);
        }

        internal static AssemblyNameFlags ExtractAssemblyNameFlags(AssemblyNameFlags combinedFlags)
        {
            return combinedFlags & unchecked((AssemblyNameFlags)0xFFFFF10F);
        }

        internal static AssemblyNameFlags CombineAssemblyNameFlags(AssemblyNameFlags flags, AssemblyContentType contentType, ProcessorArchitecture processorArchitecture)
        {
            return (AssemblyNameFlags)(((int)flags) | (((int)contentType) << 9) | ((int)processorArchitecture << 4));
        }
    }
}


