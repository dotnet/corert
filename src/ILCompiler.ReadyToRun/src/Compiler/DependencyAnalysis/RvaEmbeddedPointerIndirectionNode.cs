// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    class RvaEmbeddedPointerIndirectionNode<TTarget> : EmbeddedPointerIndirectionNode<TTarget>
        where TTarget : ISortableSymbolNode
    {
        public RvaEmbeddedPointerIndirectionNode(TTarget target)
            : base(target) { }

        protected override string GetName(NodeFactory factory) => $"Embedded pointer to {Target.GetMangledName(factory.NameMangler)}";

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new[]
            {
                    new DependencyListEntry(Target, "reloc"),
                };
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            dataBuilder.RequireInitialPointerAlignment();
            dataBuilder.EmitReloc(Target, RelocType.IMAGE_REL_BASED_ADDR32NB);
        }

        protected override int ClassCode => -66002498;
    }
}
