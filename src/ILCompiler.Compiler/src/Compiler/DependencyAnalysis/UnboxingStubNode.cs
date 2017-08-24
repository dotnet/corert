// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an unboxing stub that supports calling instance methods on boxed valuetypes.
    /// </summary>
    public class UnboxingStubNode : DependencyNodeCore<NodeFactory>, IMethodNode, IExportableSymbolNode
    {
        private MethodDesc _target;
        private int _symDefinitionOffset;

        public MethodDesc Method => _target;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override bool StaticDependenciesAreComputed => true;
        int ISymbolNode.Offset => 0;
        int ISymbolDefinitionNode.Offset => _symDefinitionOffset;
        public bool RepresentsIndirectionCell => false;

        public bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsMethod(Method);

        public UnboxingStubNode(MethodDesc target)
        {
            Debug.Assert(target.GetCanonMethodTarget(CanonicalFormKind.Specific) == target);
            Debug.Assert(target.OwningType.IsValueType);
            _target = target;
            _symDefinitionOffset = -1;
        }

        public void SetSymbolDefinitionOffset(int offset)
        {
            Debug.Assert(_symDefinitionOffset == -1);
            _symDefinitionOffset = offset;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            yield return new DependencyListEntry(factory.MethodEntrypoint(_target), "Non-unboxing target of unboxing stub");
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => 
            Array.Empty<CombinedDependencyListEntry>();
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => 
            Array.Empty<CombinedDependencyListEntry>();

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("unbox_").Append(nameMangler.GetMangledMethodName(_target));
        }

        public static string GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            return "unbox_" + nameMangler.GetMangledMethodName(method);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
    }

    public partial class UnboxingStubsRegionNode : ObjectNode, ISymbolDefinitionNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;

        public override ObjectNodeSection Section => ObjectNodeSection.TextSection;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;
        public int Offset => 0;

        public UnboxingStubsRegionNode()
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__UnboxingStubsRegion__End", true);
        }

        public ISymbolDefinitionNode EndSymbol => _endSymbol;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__UnboxingStubsRegion__");
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if(relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            switch (factory.Target.Architecture)
            {
                case TargetArchitecture.X64:
                    X64.X64Emitter x64Emitter = new X64.X64Emitter(factory, relocsOnly);
                    EmitUnboxingStubsCode(factory, ref x64Emitter);
                    return x64Emitter.Builder.ToObjectData();

                case TargetArchitecture.X86:
                    X86.X86Emitter x86Emitter = new X86.X86Emitter(factory, relocsOnly);
                    EmitUnboxingStubsCode(factory, ref x86Emitter);
                    return x86Emitter.Builder.ToObjectData();

                case TargetArchitecture.ARM:
                case TargetArchitecture.ARMEL:
                    ARM.ARMEmitter armEmitter = new ARM.ARMEmitter(factory, relocsOnly);
                    EmitUnboxingStubsCode(factory, ref armEmitter);
                    return armEmitter.Builder.ToObjectData();

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
