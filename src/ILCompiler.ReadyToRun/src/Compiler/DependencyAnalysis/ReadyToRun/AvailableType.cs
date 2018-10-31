// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class AvailableType : DependencyNodeCore<NodeFactory>, IEETypeNode
    {
        private readonly TypeDesc _type;

        public AvailableType(NodeFactory factory, TypeDesc type)
        {
            _type = type;

            //
            // This check encodes rules specific to CoreRT. Ie, no function pointer classes allowed.
            // Eventually we will hit situations where this check fails when it shouldn't and we'll need to 
            // split the logic. It's a good sanity check for the time being though.
            //
            EETypeNode.CheckCanGenerateEEType(factory, type);
        }

        public TypeDesc Type => _type;

        public int Offset => 0;

        public bool RepresentsIndirectionCell => false;

        public int ClassCode => 345483495;

        public override bool InterestingForDynamicDependencyAnalysis => false;

        public override bool HasDynamicDependencies => false;

        public override bool HasConditionalStaticDependencies => false;

        public override bool StaticDependenciesAreComputed => true;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(Type, ((AvailableType)other).Type);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            if (_type.BaseType != null)
            {
                yield return new DependencyListEntry(factory.NecessaryTypeSymbol(_type.BaseType), "Base type");
            }
            if (_type.RuntimeInterfaces != null)
            {
                foreach (DefType definedInterface in _type.RuntimeInterfaces)
                {
                    yield return new DependencyListEntry(factory.NecessaryTypeSymbol(definedInterface), "Defined interface");
                }
            }
            foreach (TypeDesc typeArg in _type.Instantiation)
            {
                yield return new DependencyListEntry(factory.NecessaryTypeSymbol(typeArg), "Instantiation argument");
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;

        protected override string GetName(NodeFactory factory) => $"Available type {Type.ToString()}";
    }
}
