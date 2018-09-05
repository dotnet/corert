﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    class ExternalTypeNode : PrecodeHelperImport, IEETypeNode
    {
        private readonly TypeDesc _type;

        public ExternalTypeNode(ReadyToRunCodegenNodeFactory factory, TypeDesc type, SignatureContext signatureContext)
            : base(factory, new TypeFixupSignature(ReadyToRunFixupKind.READYTORUN_FIXUP_TypeHandle, type, signatureContext))
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

        public override int ClassCode => -1044459;

        public override bool InterestingForDynamicDependencyAnalysis => false;

        public override bool HasDynamicDependencies => false;

        public override bool HasConditionalStaticDependencies => false;

        public override bool StaticDependenciesAreComputed => true;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledTypeName(_type));
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(Type, ((ExternalTypeNode)other).Type);
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;

        protected override string GetName(NodeFactory factory) => $"Externally referenced type {Type.ToString()}";
    }
}
