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
    public static class ProjectNDependencyBehavior
    {
        // Temporary static variable to enable full analysis when using the ProjectN abi
        // When full analysis is fully supported, remove this class and field forever.
        public static bool EnableFullAnalysis = false;
    }
    
    /// <summary>
    /// Represents a symbol that is defined externally but modeled as a method
    /// in the DependencyAnalysis infrastructure during compilation that is compiled 
    /// in the current compilation process
    /// </summary>
    public class NonExternMethodSymbolNode : ExternSymbolNode, IMethodBodyNodeWithFuncletSymbols, ISpecialUnboxThunkNode, IExportableSymbolNode
    {
        private MethodDesc _method;
        private bool _isUnboxing;
        private List<DependencyListEntry> _compilationDiscoveredDependencies;
        ISymbolNode[] _funcletSymbols = Array.Empty<ISymbolNode>();
        bool _dependenciesQueried;
        bool _hasCompiledBody;
        private HashSet<GenericLookupResult> _floatingGenericLookupResults;

        public NonExternMethodSymbolNode(NodeFactory factory, MethodDesc method, bool isUnboxing)
             : base(isUnboxing ? UnboxingStubNode.GetMangledName(factory.NameMangler, method) :
                  factory.NameMangler.GetMangledMethodName(method))
        {
            _isUnboxing = isUnboxing;
            _method = method;

            // Ensure all method bodies are fully canonicalized or not at all.
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Any) || (method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method));
            Debug.Assert(!method.IsCanonicalMethod(CanonicalFormKind.Universal) || (method.GetCanonMethodTarget(CanonicalFormKind.Universal) == method));
        }

        protected override string GetName(NodeFactory factory) => "Non" + base.GetName(factory);

        public ExportForm GetExportForm(NodeFactory factory)
        {
            ExportForm exportForm = factory.CompilationModuleGroup.GetExportMethodForm(_method, IsSpecialUnboxingThunk);
            if (exportForm == ExportForm.ByName)
                return ExportForm.None; // Non-extern symbols exported by name are naturally handled by the linker
            return exportForm;
        }

        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }

        public bool IsSpecialUnboxingThunk
        {
            get
            {
                if (_isUnboxing)
                {
                    if (!_method.HasInstantiation && _method.OwningType.IsValueType && !_method.Signature.IsStatic)
                        return _method.IsCanonicalMethod(CanonicalFormKind.Any);
                }

                return false;
            }
        }
        public ISymbolNode GetUnboxingThunkTarget(NodeFactory factory)
        {
            Debug.Assert(IsSpecialUnboxingThunk);

            return factory.MethodEntrypoint(_method.GetCanonMethodTarget(CanonicalFormKind.Specific), false);
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

        public void DeferFloatingGenericLookup(GenericLookupResult lookupResult)
        {
            if (_floatingGenericLookupResults == null)
                _floatingGenericLookupResults = new HashSet<GenericLookupResult>();
            _floatingGenericLookupResults.Add(lookupResult);
        }

        protected override void OnMarked(NodeFactory factory)
        {
            // Commit all floating generic lookups associated with the method when the method
            // is proved not dead.
            if (_floatingGenericLookupResults != null)
            {
                Debug.Assert(_method.IsCanonicalMethod(CanonicalFormKind.Any));
                TypeSystemEntity canonicalOwner = _method.HasInstantiation ? (TypeSystemEntity)_method : (TypeSystemEntity)_method.OwningType;
                DictionaryLayoutNode dictLayout = factory.GenericDictionaryLayout(canonicalOwner);

                foreach (var lookupResult in _floatingGenericLookupResults)
                {
                    dictLayout.EnsureEntry(lookupResult);
                }
            }
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
                if (ProjectNDependencyBehavior.EnableFullAnalysis)
                    return HasCompiledBody;
                else
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

            if (MethodAssociatedDataNode.MethodHasAssociatedData(factory, this))
            {
                dependencies = dependencies ?? new DependencyList();
                dependencies.Add(new DependencyListEntry(factory.MethodAssociatedData(this), "Method associated data"));
            }

            return dependencies;
        }

        public override int ClassCode => -2124588118;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            NonExternMethodSymbolNode otherMethod = (NonExternMethodSymbolNode)other;
            var result = _isUnboxing.CompareTo(otherMethod._isUnboxing);
            return result != 0 ? result : comparer.Compare(_method, otherMethod._method);
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
