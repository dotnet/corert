﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// BlobIdStackTraceMethodRvaToTokenMapping - list of 8-byte pairs (method RVA-method token)
    /// </summary>
    public sealed class StackTraceMethodMappingNode : ObjectNode, ISymbolDefinitionNode
    {
        public StackTraceMethodMappingNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "_stacktrace_methodRVA_to_token_mapping_End", true);
        }

        private ObjectAndOffsetSymbolNode _endSymbol;
        public ISymbolDefinitionNode EndSymbol => _endSymbol;

        public override bool IsShareable => false;

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        protected internal override int ClassCode => (int)ObjectNodeOrder.StackTraceMethodMappingNode;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("_stacktrace_methodRVA_to_token_mapping");
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // The dependency tracking of this node currently does nothing because the data emission relies
            // the set of compiled methods which has an incomplete state during dependency tracking.
            if (relocsOnly)
            {
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
            }

            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);
            objData.AddSymbol(_endSymbol);

            RelocType reloc = factory.Target.Abi == TargetAbi.CoreRT ?
                RelocType.IMAGE_REL_BASED_RELPTR32 : RelocType.IMAGE_REL_BASED_ADDR32NB;

            foreach (var mappingEntry in factory.MetadataManager.GetStackTraceMapping(factory))
            {
                objData.EmitReloc(factory.MethodEntrypoint(mappingEntry.Entity), reloc);
                objData.EmitInt(mappingEntry.MetadataHandle);
            }

            _endSymbol.SetSymbolOffset(objData.CountBytes);
            return objData.ToObjectData();
        }
    }
}
