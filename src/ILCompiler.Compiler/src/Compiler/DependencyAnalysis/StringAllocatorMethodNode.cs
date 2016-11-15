// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// NEWOBJ operation on String type is actually a call to a static method that returns a String
    /// instance (i.e. there's an explicit call to the runtime allocator from the static method body).
    /// This node is used to model the behavior. It represents the symbol for the target allocator
    /// method and makes sure the String type is marked as constructed.
    /// </summary>
    class StringAllocatorMethodNode : DependencyNodeCore<NodeFactory>, IMethodNode
    {
        private MethodDesc _allocationMethod;

        public MethodDesc Method => _allocationMethod;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(NodeFactory.NameMangler.GetMangledMethodName(_allocationMethod));
        }
        public int Offset => 0;

        public StringAllocatorMethodNode(MethodDesc constructorMethod)
        {
            Debug.Assert(constructorMethod.IsConstructor && constructorMethod.OwningType.IsString);

            // Find the allocator method that matches the constructor signature.
            var signatureBuilder = new MethodSignatureBuilder(constructorMethod.Signature);
            signatureBuilder.Flags = MethodSignatureFlags.Static;
            signatureBuilder.ReturnType = constructorMethod.OwningType;

            _allocationMethod = constructorMethod.OwningType.GetKnownMethod("Ctor", signatureBuilder.ToSignature());
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            return new[] {
                new DependencyListEntry(
                    factory.ConstructedTypeSymbol(factory.TypeSystemContext.GetWellKnownType(WellKnownType.String)),
                    "String constructor call"),
                new DependencyListEntry(
                    factory.MethodEntrypoint(_allocationMethod),
                    "String constructor call") };
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;

        protected override string GetName() => this.GetMangledName();
    }
}
