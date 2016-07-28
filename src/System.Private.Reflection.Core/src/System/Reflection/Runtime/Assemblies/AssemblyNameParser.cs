// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace System.Reflection.Runtime.Assemblies
{
    //
    // Parses an assembly name.
    //
    internal static class AssemblyNameParser
    {
        internal static void Parse(AssemblyName blank, String s)
        {
            if (s == null)
                throw new ArgumentNullException();
            RuntimeAssemblyName runtimeAssemblyName = Parse(s);
            runtimeAssemblyName.CopyToAssemblyName(blank);
        }

        internal static RuntimeAssemblyName Parse(String s)
        {
            Debug.Assert(s != null);
            AssemblyNameLexer lexer = new AssemblyNameLexer(s);

            // Name must come first.
            String name;
            AssemblyNameLexer.Token token = lexer.GetNext(out name);
            if (token != AssemblyNameLexer.Token.String)
            {
                if (token == AssemblyNameLexer.Token.End)
                    throw new ArgumentException(SR.Format_StringZeroLength);
                else
                    throw new FileLoadException();
            }

            if (name == String.Empty)
                throw new FileLoadException();

            Version version = null;
            String cultureName = null;
            byte[] pkt = null;
            AssemblyNameFlags flags = 0;

            LowLevelList<String> alreadySeen = new LowLevelList<String>();
            token = lexer.GetNext();
            while (token != AssemblyNameLexer.Token.End)
            {
                if (token != AssemblyNameLexer.Token.Comma)
                    throw new FileLoadException();
                String attributeName;

                token = lexer.GetNext(out attributeName);
                if (token != AssemblyNameLexer.Token.String)
                    throw new FileLoadException();
                token = lexer.GetNext();

                // Compat note: Inside AppX apps, the desktop CLR's AssemblyName parser skips past any elements that don't follow the "<Something>=<Something>" pattern.
                //  (when running classic Windows apps, such an illegal construction throws an exception as expected.)
                // Naturally, at least one app unwittingly takes advantage of this.
                if (token == AssemblyNameLexer.Token.Comma || token == AssemblyNameLexer.Token.End)
                    continue;

                if (token != AssemblyNameLexer.Token.Equals)
                    throw new FileLoadException();
                String attributeValue;
                token = lexer.GetNext(out attributeValue);
                if (token != AssemblyNameLexer.Token.String)
                    throw new FileLoadException();

                if (attributeName == String.Empty)
                    throw new FileLoadException();

                for (int i = 0; i < alreadySeen.Count; i++)
                {
                    if (alreadySeen[i].Equals(attributeName, StringComparison.OrdinalIgnoreCase))
                        throw new FileLoadException(); // Cannot specify the same attribute twice.
                }
                alreadySeen.Add(attributeName);

                if (attributeName.Equals("Version", StringComparison.OrdinalIgnoreCase))
                {
                    version = ParseVersion(attributeValue);
                }

                if (attributeName.Equals("Culture", StringComparison.OrdinalIgnoreCase))
                {
                    cultureName = ParseCulture(attributeValue);
                }

                if (attributeName.Equals("PublicKeyToken", StringComparison.OrdinalIgnoreCase))
                {
                    pkt = ParsePKT(attributeValue);
                }

                if (attributeName.Equals("ProcessorArchitecture", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= (AssemblyNameFlags)(((int)ParseProcessorArchitecture(attributeValue)) << 4);
                }

                if (attributeName.Equals("Retargetable", StringComparison.OrdinalIgnoreCase))
                {
                    if (attributeValue.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                        flags |= AssemblyNameFlags.Retargetable;
                    else if (attributeValue.Equals("No", StringComparison.OrdinalIgnoreCase))
                    {
                        // nothing to do
                    }
                    else
                        throw new FileLoadException();
                }

                if (attributeName.Equals("ContentType", StringComparison.OrdinalIgnoreCase))
                {
                    if (attributeValue.Equals("WindowsRuntime", StringComparison.OrdinalIgnoreCase))
                        flags |= (AssemblyNameFlags)(((int)AssemblyContentType.WindowsRuntime) << 9);
                    else
                        throw new FileLoadException();
                }

                // Desktop compat: If we got here, the attribute name is unknown to us. Ignore it (as long it's not duplicated.)
                token = lexer.GetNext();
            }
            return new RuntimeAssemblyName(name, version, cultureName, flags, pkt);
        }

        private static Version ParseVersion(String attributeValue)
        {
            String[] parts = attributeValue.Split('.');
            if (parts.Length > 4)
                throw new FileLoadException();
            ushort[] versionNumbers = new ushort[4];
            for (int i = 0; i < versionNumbers.Length; i++)
            {
                if (i >= parts.Length)
                    versionNumbers[i] = ushort.MaxValue;
                else
                {
                    // Desktop compat: TryParse is a little more forgiving than Fusion.
                    for (int j = 0; j < parts[i].Length; j++)
                    {
                        if (!Char.IsDigit(parts[i][j]))
                            throw new FileLoadException();
                    }
                    if (!(ushort.TryParse(parts[i], out versionNumbers[i])))
                    {
                        if (parts[i] == string.Empty)
                        {
                            // Desktop compat: Empty strings are a synonym for 0
                            versionNumbers[i] = 0;
                        }
                        else
                        {
                            throw new FileLoadException();
                        }
                    }
                }
            }

            if (parts.Length == 1)
                return null;  // Desktop compat: if only major version present, treat as no version.

            return new Version(versionNumbers[0], versionNumbers[1], versionNumbers[2], versionNumbers[3]);
        }

        private static String ParseCulture(String attributeValue)
        {
            if (attributeValue.Equals("Neutral", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }
            else
            {
                CultureInfo culture = new CultureInfo(attributeValue); // Force a CultureNotFoundException if not a valid culture.
                return culture.Name;
            }
        }

        private static byte[] ParsePKT(String attributeValue)
        {
            if (attributeValue.Equals("null", StringComparison.OrdinalIgnoreCase) || attributeValue == String.Empty)
                return Array.Empty<byte>();

            if (attributeValue.Length != 8 * 2)
                throw new FileLoadException();

            byte[] pkt = new byte[8];
            int srcIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                char hi = attributeValue[srcIndex++];
                char lo = attributeValue[srcIndex++];
                pkt[i] = (byte)((ParseHexNybble(hi) << 4) | ParseHexNybble(lo));
            }
            return pkt;
        }

        private static ProcessorArchitecture ParseProcessorArchitecture(String attributeValue)
        {
            if (attributeValue.Equals("msil", StringComparison.OrdinalIgnoreCase))
                return ProcessorArchitecture.MSIL;
            if (attributeValue.Equals("x86", StringComparison.OrdinalIgnoreCase))
                return ProcessorArchitecture.X86;
            if (attributeValue.Equals("ia64", StringComparison.OrdinalIgnoreCase))
                return ProcessorArchitecture.IA64;
            if (attributeValue.Equals("amd64", StringComparison.OrdinalIgnoreCase))
                return ProcessorArchitecture.Amd64;
            if (attributeValue.Equals("arm", StringComparison.OrdinalIgnoreCase))
                return ProcessorArchitecture.Arm;
            throw new FileLoadException();
        }

        private static byte ParseHexNybble(char c)
        {
            if (c >= '0' && c <= '9')
                return (byte)(c - '0');
            if (c >= 'a' && c <= 'f')
                return (byte)(c - 'a' + 10);
            if (c >= 'A' && c <= 'F')
                return (byte)(c - 'A' + 10);
            throw new FileLoadException();
        }
    }
}
