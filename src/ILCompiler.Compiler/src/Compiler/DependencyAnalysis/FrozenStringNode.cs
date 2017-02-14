// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class FrozenStringNode : EmbeddedObjectNode, ISymbolNode
    {
        private string _data;
        private int _syncBlockSize;

        public FrozenStringNode(string data, TargetDetails target)
        {
            _data = data;
            _syncBlockSize = target.PointerSize;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__Str_").Append(NodeFactory.NameMangler.GetMangledStringName(_data));
        }

        public override bool StaticDependenciesAreComputed => true;

        public override int Offset
        {
            get
            {
                // The frozen string symbol points at the EEType portion of the object, skipping over the sync block
                return base.Offset + _syncBlockSize;
            }
        }

        private static IEETypeNode GetEETypeNode(NodeFactory factory)
        {
            DefType systemStringType = factory.TypeSystemContext.GetWellKnownType(WellKnownType.String);

            //
            // The GC requires a direct reference to frozen objects' EETypes. If System.String will be compiled into a separate
            // binary, it must be cloned into this one.
            //
            if (factory.CompilationModuleGroup.ShouldReferenceThroughImportTable(systemStringType))
            {
                return factory.ConstructedClonedTypeSymbol(systemStringType);
            }
            else
            {
                return factory.ConstructedTypeSymbol(systemStringType);
            }
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            dataBuilder.EmitZeroPointer(); // Sync block

            dataBuilder.EmitPointerReloc(GetEETypeNode(factory));

            dataBuilder.EmitInt(_data.Length);

            foreach (char c in _data)
            {
                dataBuilder.EmitShort((short)c);
            }

            // Null-terminate for friendliness with interop
            dataBuilder.EmitShort(0);

        }

        protected override string GetName() => this.GetMangledName();

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[]
            {
                new DependencyListEntry(GetEETypeNode(factory), "Frozen string literal EEType"),
            };
        }

        protected override void OnMarked(NodeFactory factory)
        {
            factory.FrozenSegmentRegion.AddEmbeddedObject(this);
        }
    }
}
