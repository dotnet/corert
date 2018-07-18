// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a method that is imported from the runtime library.
    /// </summary>
    public class RuntimeImportMethodNode : ExternSymbolNode, IMethodNode
    {
        private MethodDesc _method;

        public RuntimeImportMethodNode(MethodDesc method)
            : base(((EcmaMethod)method).GetRuntimeImportName())
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

        public override int ClassCode => -1173492615;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((RuntimeImportMethodNode)other)._method);
        }
    }
}
