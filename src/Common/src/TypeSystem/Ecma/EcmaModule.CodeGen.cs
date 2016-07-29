// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Internal.TypeSystem.Ecma
{
    partial class EcmaModule
    {
        /// <summary>
        /// Gets the managed entrypoint method of this module or null if the module has no managed entrypoint.
        /// </summary>
        public MethodDesc EntryPoint
        {
            get
            {
                CorHeader corHeader = PEReader.PEHeaders.CorHeader;
                if ((corHeader.Flags & CorFlags.NativeEntryPoint) != 0)
                {
                    // Entrypoint is an RVA to an unmanaged method
                    return null;
                }

                int entryPointToken = corHeader.EntryPointTokenOrRelativeVirtualAddress;
                if (entryPointToken == 0)
                {
                    // No entrypoint
                    return null;
                }

                EntityHandle handle = MetadataTokens.EntityHandle(entryPointToken);

                if (handle.Kind == HandleKind.MethodDefinition)
                {
                    return GetMethod(handle);
                }
                else if (handle.Kind == HandleKind.AssemblyFile)
                {
                    // Entrypoint not in the manifest assembly
                    throw new NotImplementedException();
                }

                // Bad metadata
                throw new BadImageFormatException();
            }
        }
    }
}
