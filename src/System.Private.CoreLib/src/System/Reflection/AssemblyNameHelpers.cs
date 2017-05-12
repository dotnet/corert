// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace System.Reflection
{
    public static partial class AssemblyNameHelpers
    {
        //
        // Converts an AssemblyName to a RuntimeAssemblyName that is free from any future mutations on the AssemblyName.
        //
        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyName assemblyName)
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
        // Ensure that the PublicKeyOrPublicKeyToken (if non-null) is the short token form.
        //
        public static RuntimeAssemblyName CanonicalizePublicKeyToken(this RuntimeAssemblyName name)
        {
            AssemblyNameFlags flags = name.Flags;
            if ((flags & AssemblyNameFlags.PublicKey) == 0)
                return name;

            flags &= ~AssemblyNameFlags.PublicKey;
            byte[] publicKeyOrToken = name.PublicKeyOrToken;
            if (publicKeyOrToken != null)
                publicKeyOrToken = ComputePublicKeyToken(publicKeyOrToken);

            return new RuntimeAssemblyName(name.Name, name.Version, name.CultureName, flags, publicKeyOrToken);
        }

        //
        // These helpers convert between the combined flags+contentType+processorArchitecture value and the separated parts.
        //
        // Since these are only for trusted callers, they do NOT check for out of bound bits. 
        //

        internal static AssemblyContentType ExtractAssemblyContentType(this AssemblyNameFlags flags)
        {
            return (AssemblyContentType)((((int)flags) >> 9) & 0x7);
        }

        internal static ProcessorArchitecture ExtractProcessorArchitecture(this AssemblyNameFlags flags)
        {
            return (ProcessorArchitecture)((((int)flags) >> 4) & 0x7);
        }

        public static AssemblyNameFlags ExtractAssemblyNameFlags(this AssemblyNameFlags combinedFlags)
        {
            return combinedFlags & unchecked((AssemblyNameFlags)0xFFFFF10F);
        }

        internal static AssemblyNameFlags CombineAssemblyNameFlags(AssemblyNameFlags flags, AssemblyContentType contentType, ProcessorArchitecture processorArchitecture)
        {
            return (AssemblyNameFlags)(((int)flags) | (((int)contentType) << 9) | ((int)processorArchitecture << 4));
        }
    }
}


