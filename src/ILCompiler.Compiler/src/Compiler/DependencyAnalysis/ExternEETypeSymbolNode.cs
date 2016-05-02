// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modelled as a type in the
    /// DependencyAnalysis infrastructure during compilation.
    /// </summary>
    public sealed class ExternEETypeSymbolNode : ExternSymbolNode, IEETypeNode
    {
        private TypeDesc _type;

        public ExternEETypeSymbolNode(TypeDesc type)
            : base("__EEType_" + NodeFactory.NameMangler.GetMangledTypeName(type))
        {
            _type = type;
        }

        public TypeDesc Type
        {
            get
            {
                return _type;
            }
        }
    }
}
