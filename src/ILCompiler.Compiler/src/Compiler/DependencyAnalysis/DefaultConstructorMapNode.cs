// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Dependency analysis node used to keep track of types needing an entry in the default
    /// constructor map hashtable
    /// </summary>
    internal sealed class DefaultConstructorFromLazyNode : DependencyNodeCore<NodeFactory>
    {
        public TypeDesc TypeNeedingDefaultCtor { get; }

        public DefaultConstructorFromLazyNode(TypeDesc type)
        {
            Debug.Assert(!type.IsRuntimeDeterminedSubtype);
            Debug.Assert(type == type.ConvertToCanonForm(CanonicalFormKind.Specific));
            Debug.Assert(type.GetDefaultConstructor() != null && !type.IsValueType);

            TypeNeedingDefaultCtor = type;
        }

        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName(NodeFactory factory) => "__DefaultConstructorFromLazyNode_" + factory.NameMangler.GetMangledTypeName(TypeNeedingDefaultCtor);
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            yield return new DependencyListEntry(
                context.MaximallyConstructableType(TypeNeedingDefaultCtor),
                "DefaultConstructorNode type");

            yield return new DependencyListEntry(
                context.MethodEntrypoint(TypeNeedingDefaultCtor.GetDefaultConstructor(), TypeNeedingDefaultCtor.IsValueType), 
                "DefaultConstructorNode");
        }
    }

    /// <summary>
    /// DefaultConstructorMap blob, containing information on default constructor entrypoints of all types used 
    /// by lazy generic instantiations.
    /// </summary>
    internal class DefaultConstructorMapNode : ObjectNode, ISymbolDefinitionNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;

        public DefaultConstructorMapNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__DefaultConstructor_Map_End", true);
            _externalReferences = externalReferences;
        }

        public ISymbolNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__DefaultConstructor_Map");
        }

        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => _externalReferences.Section;
        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => false;
        public override bool StaticDependenciesAreComputed => true;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        protected internal override int ClassCode => (int)ObjectNodeOrder.DefaultConstructorMapNode;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            var writer = new NativeWriter();
            var defaultConstructorHashtable = new VertexHashtable();

            Section defaultConstructorHashtableSection = writer.NewSection();
            defaultConstructorHashtableSection.Place(defaultConstructorHashtable);

            foreach (var ctorNeeded in factory.MetadataManager.GetDefaultConstructorsNeeded())
            {
                MethodDesc defaultCtor = ctorNeeded.TypeNeedingDefaultCtor.GetDefaultConstructor();
                Debug.Assert(defaultCtor != null);

                ISymbolNode typeNode = factory.NecessaryTypeSymbol(ctorNeeded.TypeNeedingDefaultCtor);
                ISymbolNode defaultCtorNode = factory.MethodEntrypoint(defaultCtor, false);

                Vertex vertex = writer.GetTuple(
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(typeNode)),
                    writer.GetUnsignedConstant(_externalReferences.GetIndex(defaultCtorNode)));

                int hashCode = ctorNeeded.TypeNeedingDefaultCtor.GetHashCode();
                defaultConstructorHashtable.Append((uint)hashCode, defaultConstructorHashtableSection.Place(vertex));
            }

            byte[] hashTableBytes = writer.Save();

            _endSymbol.SetSymbolOffset(hashTableBytes.Length);

            return new ObjectData(hashTableBytes, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this, _endSymbol });
        }
    }
}
