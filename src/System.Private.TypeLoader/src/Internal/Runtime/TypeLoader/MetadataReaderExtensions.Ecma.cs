// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;

using Internal.Runtime.Augments;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime.General
{
    //
    // Collect various metadata reading tasks for better chunking...
    //
    public static class EcmaMetadataReaderExtensions
    {
        public static string GetString(this StringHandle handle, MetadataReader reader)
        {
            return reader.GetString(handle);
        }

        public static string GetStringOrNull(this StringHandle handle, MetadataReader reader)
        {
            if (handle.IsNil)
                return null;

            return reader.GetString(handle);
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyDefinition assemblyDefinition, MetadataReader reader)
        {
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                assemblyDefinition.Name,
                assemblyDefinition.Version,
                assemblyDefinition.Culture,
                assemblyDefinition.PublicKey,
                assemblyDefinition.Flags
                );
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyReferenceHandle assemblyReferenceHandle, MetadataReader reader)
        {
            AssemblyReference assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                assemblyReference.Name,
                assemblyReference.Version,
                assemblyReference.Culture,
                assemblyReference.PublicKeyOrToken,
                assemblyReference.Flags
                );
        }

        private static RuntimeAssemblyName CreateRuntimeAssemblyNameFromMetadata(
            MetadataReader reader,
            StringHandle name,
            Version version,
            StringHandle culture,
            BlobHandle publicKeyOrToken,
            AssemblyFlags assemblyFlags)
        {
            AssemblyNameFlags assemblyNameFlags = AssemblyNameFlags.None;
            if (0 != (assemblyFlags & AssemblyFlags.PublicKey))
                assemblyNameFlags |= AssemblyNameFlags.PublicKey;
            if (0 != (assemblyFlags & AssemblyFlags.Retargetable))
                assemblyNameFlags |= AssemblyNameFlags.Retargetable;
            int contentType = ((int)assemblyFlags) & 0x00000E00;
            assemblyNameFlags |= (AssemblyNameFlags)contentType;

            byte[] publicKeyOrTokenByteArray;
            if (!publicKeyOrToken.IsNil)
            {
                ImmutableArray<byte> publicKeyOrTokenBlob = reader.GetBlobContent(publicKeyOrToken);
                publicKeyOrTokenByteArray = new byte[publicKeyOrTokenBlob.Length];
                publicKeyOrTokenBlob.CopyTo(publicKeyOrTokenByteArray);
            }
            else
            {
                publicKeyOrTokenByteArray = Array.Empty<byte>();
            }
            
            string cultureName = culture.GetString(reader);
            if (!String.IsNullOrEmpty(cultureName))
            {
                // Canonicalize spelling and force a CultureNotFoundException if not a valid culture
                CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureName);
                cultureName = cultureInfo.Name;
            }

            return new RuntimeAssemblyName(
                name.GetString(reader),
                version,
                cultureName,
                assemblyNameFlags,
                publicKeyOrTokenByteArray
                );
        }
    }
}
