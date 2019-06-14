// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class ImportedThreadStaticsIndexNode : ExternSymbolNode
    {
        public ImportedThreadStaticsIndexNode(NodeFactory factory)
            : base("__imp_" + ThreadStaticsIndexNode.GetMangledName(((UTCNameMangler)factory.NameMangler).GetImportedTlsIndexPrefix()))
        {
        }

        public override bool RepresentsIndirectionCell => true;
    }

    public sealed class ImportedThreadStaticsOffsetNode : ExternSymbolNode
    {
        public ImportedThreadStaticsOffsetNode(MetadataType type, NodeFactory factory)
            : base("__imp_" + ThreadStaticsOffsetNode.GetMangledName(factory.NameMangler, type))
        {
        }

        public override bool RepresentsIndirectionCell => true;
    }
}