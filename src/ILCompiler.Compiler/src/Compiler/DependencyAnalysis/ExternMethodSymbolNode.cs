// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modelled as a method
    /// in the DependencyAnalysis infrastructure during compilation
    /// </summary>
    public sealed class ExternMethodSymbolNode : ExternSymbolNode, IMethodNode
    {
        private MethodDesc _method;

        public ExternMethodSymbolNode(MethodDesc method) : base(NodeFactory.NameMangler.GetMangledMethodName(method))
        {
            _method = method;
        }

        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }
    }
}
