// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modeled as a method
    /// in the DependencyAnalysis infrastructure during compilation that is compiled 
    /// in the current compilation process
    /// </summary>
    public class NonExternMethodSymbolNode : ExternSymbolNode, IMethodNode
    {
        private MethodDesc _method;
        private List<DependencyListEntry> _compilationDiscoveredDependencies;
        bool _dependenciesQueried;
        bool _hasCompiledBody;

        public NonExternMethodSymbolNode(NodeFactory factory, MethodDesc method, bool isUnboxing)
            : base(isUnboxing ? UnboxingStubNode.GetMangledName(factory.NameMangler, method) :
                  factory.NameMangler.GetMangledMethodName(method))
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

        public bool HasCompiledBody => _hasCompiledBody;
        public void SetHasCompiledBody()
        {
            // This method isn't expected to be called multiple times
            Debug.Assert(!_hasCompiledBody);
            _hasCompiledBody = true;
        }

        public void AddCompilationDiscoveredDependency(DependencyNodeCore<NodeFactory> node, string reason)
        {
            Debug.Assert(!_dependenciesQueried);
            if (_compilationDiscoveredDependencies == null)
                _compilationDiscoveredDependencies = new List<DependencyListEntry>();
            _compilationDiscoveredDependencies.Add(new DependencyNodeCore<NodeFactory>.DependencyListEntry(node, reason));
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                // TODO Change this to
                // return HasCompiledBody;
                // when we fix up creation of NonExternMethodSymbolNode to be correctly handled
                return true;
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            _dependenciesQueried = true;
            DependencyList dependencies = null;
            CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);

            if (_compilationDiscoveredDependencies != null)
                dependencies.AddRange(_compilationDiscoveredDependencies);

            return dependencies;
        }
    }
}
