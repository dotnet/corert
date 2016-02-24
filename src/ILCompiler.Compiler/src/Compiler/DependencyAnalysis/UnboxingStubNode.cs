// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an unboxing stub that supports calling instance methods on boxed valuetypes.
    /// </summary>
    public partial class UnboxingStubNode : AssemblyStubNode, IMethodNode
    {
        private MethodDesc _target;

        public MethodDesc Method
        {
            get
            {
                return _target;
            }
        }

        public UnboxingStubNode(MethodDesc target)
        {
            Debug.Assert(target.OwningType.IsValueType);
            _target = target;
        }

        public override string MangledName
        {
            get
            {
                return "unbox_" + NodeFactory.NameMangler.GetMangledMethodName(_target);
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }
    }
}
