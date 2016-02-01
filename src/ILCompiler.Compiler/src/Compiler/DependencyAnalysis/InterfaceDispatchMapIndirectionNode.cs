// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ILCompiler.DependencyAnalysis
{
    class InterfaceDispatchMapIndirectionNode : EmbeddedObjectNode, ISymbolNode
    {
        TypeDesc _type;

        public InterfaceDispatchMapIndirectionNode(TypeDesc type)
        {
            _type = type;
            base.Offset = InvalidOffset;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                if (Offset == InvalidOffset)
                {
                    throw new InvalidOperationException("MangledName called before Offset was initialized.");
                }
                
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__DispatchMap_Pointer_" + base.Offset.ToString(CultureInfo.InvariantCulture);
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return Offset;
            }
        }

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.RequirePointerAlignment();
            builder.EmitPointerReloc(factory.InterfaceDispatchMap(_type));
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(context.DispatchMapTable, "Dispatch Map Table"),
                                               new DependencyListEntry(context.InterfaceDispatchMap(_type), "Referenced interface dispatch map")};
        }
    }
}
