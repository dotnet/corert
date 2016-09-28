// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class ClonedConstructedEETypeNode : ConstructedEETypeNode, ISymbolNode
    {
        public ClonedConstructedEETypeNode(TypeDesc type) : base(type)
        {
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName + " cloned";
        }

        //
        // A cloned type must be named differently than the type it is a clone of so the linker
        // will have an unambiguous symbol to resolve
        //
        string ISymbolNode.MangledName
        {
            get
            {
                return GetMangledName(_type) + "_Clone";
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        protected override void OutputRelatedType(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            //
            // Cloned types use the related type field to point via an IAT slot at their true implementation
            //
            objData.EmitPointerReloc(factory.NecessaryTypeSymbol(_type));
        }
    }
}
