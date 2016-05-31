// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Internal.TypeSystem.Ecma
{
    // Pluggable file that adds PDB handling functionality to EcmaModule
    partial class EcmaModule
    {
        public PdbSymbolReader PdbReader
        {
            get; private set;
        }

        internal EcmaModule(TypeSystemContext context, PEReader peReader, MetadataReader metadataReader, PdbSymbolReader pdbReader)
            : this(context, peReader, metadataReader)
        {
            PdbReader = pdbReader;
        }

        public static EcmaModule Create(TypeSystemContext context, PEReader peReader, PdbSymbolReader pdbReader)
        {
            MetadataReader metadataReader = CreateMetadataReader(context, peReader);

            if (metadataReader.IsAssembly)
                return new EcmaAssembly(context, peReader, metadataReader, pdbReader);
            else
                return new EcmaModule(context, peReader, metadataReader, pdbReader);
        }
    }
}
