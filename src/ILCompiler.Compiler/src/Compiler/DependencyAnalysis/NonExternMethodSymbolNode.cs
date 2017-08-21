// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol that is defined externally but modeled as a method
    /// in the DependencyAnalysis infrastructure during compilation that is compiled 
    /// in the current compilation process
    /// </summary>
    public class NonExternMethodSymbolNode : ExternSymbolNode, IMethodBodyNodeWithFuncletSymbols
    {
        private MethodDesc _method;
        private List<DependencyListEntry> _compilationDiscoveredDependencies;
        ISymbolNode[] _funcletSymbols = Array.Empty<ISymbolNode>();
        bool _dependenciesQueried;
        bool _hasCompiledBody;

        public NonExternMethodSymbolNode(NodeFactory factory, MethodDesc method, bool isUnboxing)
            : base(isUnboxing ? UnboxingStubNode.GetMangledName(factory.NameMangler, method) :
                  factory.NameMangler.GetMangledMethodName(method))
        {
            _method = method;
        }

        protected override string GetName(NodeFactory factory) => "Non" + base.GetName(factory);

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

        public void SetFuncletCount(int funcletCount)
        {
            Debug.Assert(funcletCount > 0);
            Debug.Assert(_funcletSymbols.Length == 0);
            ISymbolNode[] funclets = new ISymbolNode[funcletCount];
            for (int funcletId = 1; funcletId <= funcletCount; funcletId++)
                funclets[funcletId - 1] = new FuncletSymbol(this, funcletId);
            _funcletSymbols = funclets;
        }

        public void AddCompilationDiscoveredDependency(IDependencyNode<NodeFactory> node, string reason)
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

        ISymbolNode[] IMethodBodyNodeWithFuncletSymbols.FuncletSymbols
        {
            get
            {
                return _funcletSymbols;
            }
        }
        
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            _dependenciesQueried = true;
            DependencyList dependencies = null;
            CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);

            if (_compilationDiscoveredDependencies != null)
            {
                dependencies = dependencies ?? new DependencyList();
                dependencies.AddRange(_compilationDiscoveredDependencies);
            }

            return dependencies;
        }

        private class FuncletSymbol : ISymbolNodeWithFuncletId
        {
            public FuncletSymbol(NonExternMethodSymbolNode methodSymbol, int funcletId)
            {
                _funcletId = funcletId;
                _methodSymbol = methodSymbol;
            }

            private int _funcletId;
            private NonExternMethodSymbolNode _methodSymbol;

            public ISymbolNode AssociatedMethodSymbol => _methodSymbol;

            public int FuncletId => _funcletId;

            public int Offset => 0;

            public bool RepresentsIndirectionCell => false;
            public bool InterestingForDynamicDependencyAnalysis => false;
            public bool HasDynamicDependencies => false;
            public bool HasConditionalStaticDependencies => false;
            public bool StaticDependenciesAreComputed => true;
            public bool Marked => _methodSymbol.Marked;
            public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
            {
                _methodSymbol.AppendMangledName(nameMangler, sb);
            }

            public IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
            {
                return null;
            }

            public IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
            {
                return null;
            }

            public IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
            {
                return null;
            }
        }
    }
}
