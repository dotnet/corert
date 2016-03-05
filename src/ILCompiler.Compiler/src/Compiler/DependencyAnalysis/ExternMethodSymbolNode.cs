// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ILCompiler.DependencyAnalysisFramework;
using System;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally and statically linked to the output obj file but
    /// modelled as a method in the DependencyAnalysis infrastructure during compilation
    /// </summary>
    public class ExternMethodSymbolNode : ExternSymbolNode, IMethodNode
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
