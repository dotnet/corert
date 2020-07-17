// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    public partial class RuntimeDecodableJumpStubNode : JumpStubNode, IMethodNode
    {
        private IMethodNode WrappedMethodIndirectionCellNode => (IMethodNode)Target;

        public MethodDesc Method => WrappedMethodIndirectionCellNode.Method;

        public override ObjectNodeSection Section
        {
            get
            {
                // Use the unboxing stub node section. This allows the logic in RhGetCodeTarget to identify that it should be able to decode this stub
                // TODO rename these sections to make it obvious these are jump stubs as well as unboxings stubs
                TargetDetails targetDetails = WrappedMethodIndirectionCellNode.Method.Context.Target;
                string sectionName = targetDetails.IsWindows ? UnboxingStubNode.WindowsSectionName : UnboxingStubNode.UnixSectionName;
                return new ObjectNodeSection(sectionName, SectionType.Executable);
            }
        }
        public override bool IsShareable => true;

        public RuntimeDecodableJumpStubNode(IMethodNode target) : base(target)
        {
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            string name = WrappedMethodIndirectionCellNode.GetMangledName(nameMangler);
            Debug.Assert(name.StartsWith("__mrt__"));
            // Add a __imp__ prefix to indicate this is a stub to the debugger
            sb.Append("__imp__");
            sb.Append(name.Substring("__mrt__".Length));
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override int ClassCode => 532434339;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(WrappedMethodIndirectionCellNode, ((RuntimeDecodableJumpStubNode)other).WrappedMethodIndirectionCellNode);
        }
    }
}
