// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an unboxing stub that supports calling instance methods on boxed valuetypes.
    /// </summary>
    public partial class UnboxingStubNode : AssemblyStubNode, IMethodNode, IExportableSymbolNode
    {
        private MethodDesc _target;

        public MethodDesc Method
        {
            get
            {
                return _target;
            }
        }

        public bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsMethod(Method);

        public UnboxingStubNode(MethodDesc target)
        {
            Debug.Assert(target.GetCanonMethodTarget(CanonicalFormKind.Specific) == target);
            Debug.Assert(target.OwningType.IsValueType);
            _target = target;
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("unbox_").Append(nameMangler.GetMangledMethodName(_target));
        }

        public static string GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            return "unbox_" + nameMangler.GetMangledMethodName(method);
        }

        public override bool IsShareable => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
    }
}
