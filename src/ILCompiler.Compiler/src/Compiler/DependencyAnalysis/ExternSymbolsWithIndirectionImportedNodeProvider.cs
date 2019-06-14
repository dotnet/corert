// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class ExternSymbolsWithIndirectionImportedNodeProvider : ImportedNodeProvider
    {
        public override IEETypeNode ImportedEETypeNode(NodeFactory factory, TypeDesc type)
        {
            return new ImportedEETypeSymbolNode(factory, type);
        }

        public override ISortableSymbolNode ImportedGCStaticNode(NodeFactory factory, MetadataType type)
        {
            return new ImportedGCStaticsNode(factory, type);
        }

        public override ISortableSymbolNode ImportedNonGCStaticNode(NodeFactory factory, MetadataType type)
        {
            return new ImportedNonGCStaticsNode(factory, type);
        }

        public override ISortableSymbolNode ImportedThreadStaticOffsetNode(NodeFactory factory, MetadataType type)
        {
            return new ImportedThreadStaticsOffsetNode(type, factory);
        }

        public override ISortableSymbolNode ImportedThreadStaticIndexNode(NodeFactory factory, MetadataType type)
        {
            return new ImportedThreadStaticsIndexNode(factory);
        }

        public override ISortableSymbolNode ImportedTypeDictionaryNode(NodeFactory factory, TypeDesc type)
        {
            return new ImportedTypeGenericDictionaryNode(factory, type);
        }

        public override ISortableSymbolNode ImportedMethodDictionaryNode(NodeFactory factory, MethodDesc method)
        {
            return new ImportedMethodGenericDictionaryNode(factory, method);
        }

        public override IMethodNode ImportedMethodCodeNode(NodeFactory factory, MethodDesc method, bool unboxingStub)
        {
            return new ExternMethodSymbolNode(factory, method, unboxingStub);
        }
    }
}
