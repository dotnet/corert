// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modelled as a method
    /// in the DependencyAnalysis infrastructure during compilation that is compiled 
    /// in the current compilation process
    /// </summary>
    public class NonExternMethodSymbolNode : ExternSymbolNode, IMethodNode
    {
        private MethodDesc _method;

        public NonExternMethodSymbolNode(MethodDesc method) : base(NodeFactory.NameMangler.GetMangledMethodName(method))
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

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyList dependencies = null;
            CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);
            return dependencies;
        }
    }
}
