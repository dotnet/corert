// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;
using System.Collections.Generic;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the offset of the thread static region of a given type from the TLS section start. 
    /// The node is used for cross-module thread statics reference
    /// </summary>
    public class ThreadStaticsOffsetNode : EmbeddedObjectNode, IExportableSymbolNode, ISortableSymbolNode
    {
        private MetadataType _type;

        public ThreadStaticsOffsetNode(MetadataType type, NodeFactory factory)
        {
            _type = type;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(nameMangler, _type));
        }

        public static string GetMangledName(NameMangler nameMangler, TypeDesc type)
        {
           return "__ThreadStaticBaseOffset_" + nameMangler.GetMangledTypeName(type);
        }            

        protected override void OnMarked(NodeFactory factory)
        {
            (factory as UtcNodeFactory).ThreadStaticsOffsetRegion.AddEmbeddedObject(this);
        }
        
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            UtcNodeFactory hostedFactory = factory as UtcNodeFactory;
            Debug.Assert(hostedFactory != null);
            DependencyListEntry[] result = new DependencyListEntry[2];
            result[0] = new DependencyListEntry(hostedFactory.ThreadStaticsOffsetRegion, "ThreadStatics Offset Region");
            result[1] = new DependencyListEntry(factory.TypeThreadStaticsSymbol(_type), "ThreadStatics Base");
            return result;
        }

        public ExportForm GetExportForm(NodeFactory factory)
        {
            return factory.CompilationModuleGroup.GetExportTypeForm(Type);
        }

        public MetadataType Type => _type;

        int ISymbolNode.Offset => 0;

        int ISymbolDefinitionNode.Offset => OffsetFromBeginningOfArray;

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.EmitReloc(factory.TypeThreadStaticsSymbol(_type), RelocType.IMAGE_REL_SECREL);
        }

        public sealed override int ClassCode => 419394032;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_type, ((ThreadStaticsOffsetNode)other)._type);
        }
    }
}
