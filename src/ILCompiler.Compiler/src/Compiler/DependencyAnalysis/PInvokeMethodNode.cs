// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a raw PInvoke method entry point.
    /// </summary>
    public sealed class PInvokeMethodNode : ExternSymbolNode, IMethodNode
    {
        private MethodDesc _target;

        public PInvokeMethodNode(MethodDesc target)
            : base(target.GetPInvokeMethodMetadata().Name)
        {
            Debug.Assert(target.DetectSpecialMethodKind() == SpecialMethodKind.PInvoke);
            _target = target;
        }

        public MethodDesc Method
        {
            get
            {
                return _target;
            }
        }
    }
}
