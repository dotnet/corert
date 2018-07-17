// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an unboxing stub that supports calling instance methods on boxed valuetypes.
    /// </summary>
    public partial class UnboxingStubNode : AssemblyStubNode, IMethodNode, IExportableSymbolNode
    {
        // Section name on Windows has to be alphabetically less than the ending WindowsUnboxingStubsRegionNode node, and larger than
        // the begining WindowsUnboxingStubsRegionNode node, in order to have proper delimiters to the begining/ending of the
        // stubs region, in order for the runtime to know where the region starts and ends.
        internal static readonly string WindowsSectionName = ".unbox$M";
        internal static readonly string UnixSectionName = "__unbox";

        private readonly TargetDetails _targetDetails;

        public MethodDesc Method { get; }

        public override ObjectNodeSection Section
        {
            get
            {
                string sectionName = _targetDetails.IsWindows ? WindowsSectionName : UnixSectionName;
                return new ObjectNodeSection(sectionName, SectionType.Executable);
            }
        }
        public override bool IsShareable => true;

        public ExportForm GetExportForm(NodeFactory factory) => factory.CompilationModuleGroup.GetExportMethodForm(Method, true);

        public UnboxingStubNode(MethodDesc target, TargetDetails targetDetails)
        {
            Debug.Assert(target.GetCanonMethodTarget(CanonicalFormKind.Specific) == target);
            Debug.Assert(target.OwningType.IsValueType);
            Method = target;
            _targetDetails = targetDetails;
        }

        private ISymbolNode GetUnderlyingMethodEntrypoint(NodeFactory factory)
        {
            ISymbolNode node = factory.MethodEntrypoint(Method);
            if (node is RuntimeDecodableJumpStubNode)
            {
                return ((RuntimeDecodableJumpStubNode)node).Target;
            }
            return node;
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("unbox_").Append(nameMangler.GetMangledMethodName(Method));
        }

        public static string GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            return "unbox_" + nameMangler.GetMangledMethodName(method);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override int ClassCode => -1846923013;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(Method, ((UnboxingStubNode)other).Method);
        }
    }

    //
    // On Windows, we need to create special start/stop sections, in order to group all the unboxing stubs and
    // have delimiters accessible through extern "C" variables in the bootstrapper. On Linux/Apple, the linker provides 
    // special names to the begining and end of sections already.
    //
    public class WindowsUnboxingStubsRegionNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly bool _isEndSymbol;

        public override ObjectNodeSection Section => new ObjectNodeSection(".unbox$" + (_isEndSymbol? "Z" : "A"), SectionType.Executable);
        public override bool IsShareable => true;
        public override bool StaticDependenciesAreComputed => true;
        public int Offset => 0;

        public WindowsUnboxingStubsRegionNode(bool isEndSymbol)
        {
            _isEndSymbol = isEndSymbol;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__unbox_" + (_isEndSymbol ? "z" : "a"));
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            Debug.Assert(factory.Target.IsWindows);

            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
            objData.AddSymbol(this);

            return objData.ToObjectData();
        }

        public override int ClassCode => 1102274050;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _isEndSymbol.CompareTo(((WindowsUnboxingStubsRegionNode)other)._isEndSymbol);
        }
    }
}
