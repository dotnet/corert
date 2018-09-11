// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    class SyntheticSymbolNode : ISymbolDefinitionNode
    {
        MethodWithGCInfo _method;

        ObjectNode.ObjectData _ehInfo;

        public SyntheticSymbolNode(MethodWithGCInfo method, ObjectNode.ObjectData ehInfo)
        {
            _method = method;
            _ehInfo = ehInfo;
        }

        public int Offset => 0;

        public bool RepresentsIndirectionCell => false;

        public bool InterestingForDynamicDependencyAnalysis => false;

        public bool HasDynamicDependencies => false;

        public bool HasConditionalStaticDependencies => false;

        public bool StaticDependenciesAreComputed => true;

        public bool Marked => true;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("SyntheticSymbolNode->");
            _method.AppendMangledName(nameMangler, sb);
        }

        public IEnumerable<DependencyNodeCore<NodeFactory>.DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            foreach (Relocation reloc in _ehInfo.Relocs)
            {
                yield return new DependencyNodeCore<NodeFactory>.DependencyListEntry(reloc.Target, "EHInfo reloc");
            }
        }

        public IEnumerable<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;
        public IEnumerable<DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;
    }

    public class ExceptionInfoLookupTableNode : HeaderTableNode, IEnumerable<ObjectNode.ObjectData>
    {
        private List<MethodWithGCInfo> _methodNodes;
        private List<ObjectData> _ehInfoToEmit;

        private readonly NodeFactory _nodeFactory;

        public ExceptionInfoLookupTableNode(NodeFactory nodeFactory)
            : base(nodeFactory.Target)
        {
            _nodeFactory = nodeFactory;
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunExceptionInfoLookupTable@");
            sb.Append(Offset.ToString());
        }

        public IEnumerator<ObjectNode.ObjectData> EnumerateEHInfo()
        {
            LayoutMethodsWithEHInfo();
            return _ehInfoToEmit.GetEnumerator();
        }

        internal void LayoutMethodsWithEHInfo()
        {
            if (_methodNodes != null)
            {
                // Already initialized
                return;
            }

            _methodNodes = new List<MethodWithGCInfo>();
            _ehInfoToEmit = new List<ObjectData>();

            foreach (MethodDesc method in _nodeFactory.MetadataManager.GetCompiledMethods())
            {
                MethodWithGCInfo methodCodeNode = _nodeFactory.MethodEntrypoint(method) as MethodWithGCInfo;
                if (methodCodeNode == null)
                {
                    methodCodeNode = ((ExternalMethodImport)_nodeFactory.MethodEntrypoint(method))?.MethodCodeNode;
                    if (methodCodeNode == null)
                        continue;
                }

                ObjectData ehInfo = methodCodeNode.EHInfo;
                if (ehInfo != null && ehInfo.Data.Length != 0)
                {
                    if (ehInfo.DefinedSymbols == null || ehInfo.DefinedSymbols.Length == 0)
                    {
                        // Emit synthetic symbol to represent the EH info node
                        SyntheticSymbolNode symbol = new SyntheticSymbolNode(methodCodeNode, ehInfo);

                        ehInfo = new ObjectData(
                            ehInfo.Data,
                            ehInfo.Relocs,
                            ehInfo.Alignment,
                            new ISymbolDefinitionNode[] { symbol });
                    }

                    _methodNodes.Add(methodCodeNode);
                    _ehInfoToEmit.Add(ehInfo);
                }
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node only triggers  generation of the EH info node.
            if (relocsOnly)
            {
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
            }

            if (_methodNodes == null)
                LayoutMethodsWithEHInfo();

            ObjectDataBuilder exceptionInfoLookupBuilder = new ObjectDataBuilder(factory, relocsOnly);
            exceptionInfoLookupBuilder.RequireInitialAlignment(8);

            // Add the symbol representing this object node
            exceptionInfoLookupBuilder.AddSymbol(this);

            // First, emit the actual EH records in sequence and store map from methods to the EH record symbols
            for (int index = 0; index < _methodNodes.Count; index++)
            {
                exceptionInfoLookupBuilder.EmitReloc(_methodNodes[index], RelocType.IMAGE_REL_BASED_ADDR32NB);
                exceptionInfoLookupBuilder.EmitReloc(_ehInfoToEmit[index].DefinedSymbols[0], RelocType.IMAGE_REL_BASED_ADDR32NB);
            }

            // Sentinel record - method RVA = -1, EH info offset = total EH info size
            exceptionInfoLookupBuilder.EmitUInt(~0u);
            if (_ehInfoToEmit.Count != 0)
            {
                ObjectData lastEhInfo = _ehInfoToEmit[_ehInfoToEmit.Count - 1];
                exceptionInfoLookupBuilder.EmitReloc(lastEhInfo.DefinedSymbols[0], RelocType.IMAGE_REL_BASED_ADDR32NB, lastEhInfo.Data.Length);
            }
            else
            {
                exceptionInfoLookupBuilder.EmitUInt(0);
            }

            return exceptionInfoLookupBuilder.ToObjectData();
        }

        public IEnumerator<ObjectData> GetEnumerator()
        {
            LayoutMethodsWithEHInfo();
            return _ehInfoToEmit.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            LayoutMethodsWithEHInfo();
            return _ehInfoToEmit.GetEnumerator();
        }

        public override int ClassCode => -855231428;
    }
}
