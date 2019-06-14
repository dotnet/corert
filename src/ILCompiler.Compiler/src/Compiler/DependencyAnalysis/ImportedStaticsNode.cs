// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class ImportedGCStaticsNode : ExternSymbolNode
    {
        public ImportedGCStaticsNode(NodeFactory factory, MetadataType type)
            : base("__imp_" + GCStaticsNode.GetMangledName(type, factory.NameMangler))
        {
        }

        public override bool RepresentsIndirectionCell => true;
    }

    public sealed class ImportedNonGCStaticsNode : ExternSymbolNode
    {
        public ImportedNonGCStaticsNode(NodeFactory factory, MetadataType type)
            : base("__imp_" + NonGCStaticsNode.GetMangledName(type, factory.NameMangler))
        {
        }

        public override bool RepresentsIndirectionCell => true;
    }
}