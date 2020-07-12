// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modelled as a type in the
    /// DependencyAnalysis infrastructure during compilation. An "ImportedEETypeSymbolNode"
    /// will not be present in the final linked binary and instead referenced through
    /// an import table mechanism.
    /// </summary>
    public sealed class ImportedEETypeSymbolNode : ExternSymbolNode, IEETypeNode
    {
        private TypeDesc _type;

        public ImportedEETypeSymbolNode(NodeFactory factory, TypeDesc type)
            : base("__imp_" + factory.NameMangler.NodeMangler.EEType(type))
        {
            _type = type;
        }

        public override bool RepresentsIndirectionCell => true;

        public TypeDesc Type
        {
            get
            {
                return _type;
            }
        }

        public override int ClassCode => 395643063;
    }
}
