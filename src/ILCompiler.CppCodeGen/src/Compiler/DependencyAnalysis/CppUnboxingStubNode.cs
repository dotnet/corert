// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class CppUnboxingStubNode : DependencyNodeCore<NodeFactory>, IMethodNode
    {
        public CppUnboxingStubNode(MethodDesc method)
        {
            Debug.Assert(method.OwningType.IsValueType && !method.Signature.IsStatic);
            Method = method;
        }

        public MethodDesc Method { get; }

        public int ClassCode => 17864523;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("unbox_").Append(nameMangler.GetMangledMethodName(Method));
        }

        public int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(this.Method, ((CppUnboxingStubNode)other).Method);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new DependencyListEntry[] {
                new DependencyListEntry(factory.MethodEntrypoint(Method), "Target of unboxing") };
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public static string GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            return "unbox_" + nameMangler.GetMangledMethodName(method);
        }

        public override bool StaticDependenciesAreComputed => true;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasConditionalStaticDependencies => false;

        public int Offset => throw new System.NotImplementedException();

        public bool RepresentsIndirectionCell => throw new System.NotImplementedException();

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
        }
    }
}
