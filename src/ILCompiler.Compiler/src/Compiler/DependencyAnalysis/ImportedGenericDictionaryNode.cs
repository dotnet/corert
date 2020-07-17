// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class ImportedMethodGenericDictionaryNode : ExternSymbolNode
    {
        private MethodDesc _owningMethod;

        public ImportedMethodGenericDictionaryNode(NodeFactory factory, MethodDesc owningMethod)
            : base("__imp_" + factory.NameMangler.NodeMangler.MethodGenericDictionary(owningMethod))
        {
            _owningMethod = owningMethod;
        }

        public override bool RepresentsIndirectionCell => true;
    }

    public sealed class ImportedTypeGenericDictionaryNode : ExternSymbolNode
    {
        private TypeDesc _owningType;

        public ImportedTypeGenericDictionaryNode(NodeFactory factory, TypeDesc owningType)
            : base("__imp_" + factory.NameMangler.NodeMangler.TypeGenericDictionary(owningType))
        {
            Debug.Assert(!factory.LazyGenericsPolicy.UsesLazyGenerics(owningType));
            _owningType = owningType;
        }

        public override bool RepresentsIndirectionCell => true;
    }
}
