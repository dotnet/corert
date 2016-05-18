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

        public EcmaModule(TypeSystemContext context, PEReader peReader, PdbSymbolReader pdbReader)
            : this(context, peReader)
        {
            PdbReader = pdbReader;
        }

        public EcmaModule(TypeSystemContext context, MetadataReader metadataReader, PdbSymbolReader pdbReader)
            : this(context, metadataReader)
        {
            PdbReader = pdbReader;
        }
    }
}
