// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using Internal.Runtime.JitSupport;

using Internal.TypeSystem;
using Internal.IL;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Separate copy of NodeFactory from compiler implementation. This should be refactored to use
    /// NodeFactory as a base type, but time constraints currently prevent resourcing for that
    /// </summary>
    public class NodeFactory
    {
        private struct NodeCache<TKey, TValue>
        {
            private Func<TKey, TValue> _creator;
            private Dictionary<TKey, TValue> _cache;

            public NodeCache(Func<TKey, TValue> creator, IEqualityComparer<TKey> comparer)
            {
                _creator = creator;
                _cache = new Dictionary<TKey, TValue>(comparer);
            }

            public NodeCache(Func<TKey, TValue> creator)
            {
                _creator = creator;
                _cache = new Dictionary<TKey, TValue>();
            }

            public TValue GetOrAdd(TKey key)
            {
                TValue result;
                if (!_cache.TryGetValue(key, out result))
                {
                    result = _creator(key);
                    _cache.Add(key, result);
                }
                return result;
            }
        }

        private NodeCache<MethodDesc, IMethodNode> _methodEntrypoints;
        private NodeCache<string, JitFrozenStringNode> _frozenStrings;

        private void CreateNodeCaches()
        {
            _methodEntrypoints = new NodeCache<MethodDesc, IMethodNode>(method =>
            {
                return new JitMethodEntrypointNode(method);
            });

            _frozenStrings = new NodeCache<string, JitFrozenStringNode>(str =>
            {
                return new JitFrozenStringNode(str);
            });
        }

        TargetDetails _targetDetails;
        TypeSystemContext _typeSystemContext;

        public NodeFactory(TypeSystemContext typeSystemContext)
        {
            _targetDetails = typeSystemContext.Target;
            _typeSystemContext = typeSystemContext;
            CreateNodeCaches();
        }

        public BlobNode ReadOnlyDataBlob(string s, byte[] b, int align) { throw new NotImplementedException(); }
        public SettableReadOnlyDataBlob SettableReadOnlyDataBlob(string s) { throw new NotImplementedException(); }
        public ISymbolNode ReadyToRunHelper(ReadyToRunHelperId id, Object target) { throw new NotImplementedException(); }
        public IMethodNode MethodEntrypoint(MethodDesc method, bool unboxingStub = false)
        {
            if (unboxingStub != false)
            {
                throw new NotImplementedException();
            }

            return _methodEntrypoints.GetOrAdd(method);
        }

        public IMethodNode ExactCallableAddress(MethodDesc method, bool isUnboxingStub = false)
        {
            MethodDesc canonMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);
            if (method != canonMethod)
                return FatFunctionPointer(method, isUnboxingStub);
            else
                return MethodEntrypoint(method, isUnboxingStub);
        }

        public IMethodNode CanonicalEntrypoint(MethodDesc method, bool isUnboxingStub = false)
        {
            MethodDesc canonMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);
            if (method != canonMethod)
                return ShadowConcreteMethod(method, isUnboxingStub);
            else
                return MethodEntrypoint(method, isUnboxingStub);
        }

        public IMethodNode RuntimeDeterminedMethod(MethodDesc method) { throw new NotImplementedException(); }
        public JitFrozenStringNode SerializedStringObject(string data) { return _frozenStrings.GetOrAdd(data); }
        public JitGenericMethodDictionaryNode MethodGenericDictionary(MethodDesc method) { throw new NotImplementedException(); }
        public IEETypeNode ConstructedTypeSymbol(TypeDesc type) { throw new NotImplementedException(); }
        public IMethodNode ShadowConcreteMethod(MethodDesc method, bool isUnboxingStub = false) { throw new NotImplementedException(); }
        internal IMethodNode StringAllocator(MethodDesc stringConstructor) { throw new NotImplementedException(); }
        public ISymbolNode ExternSymbol(string name) { throw new NotImplementedException(); }
        public ISymbolNode ConstantUtf8String(string str) { throw new NotImplementedException(); }
        public IEETypeNode NecessaryTypeSymbol(TypeDesc type) { return ConstructedTypeSymbol(type); }
        internal JitInterfaceDispatchCellNode InterfaceDispatchCell(MethodDesc method) { throw new NotImplementedException(); }
        public ISymbolNode RuntimeMethodHandle(MethodDesc method) { throw new NotImplementedException(); }
        public ISymbolNode RuntimeFieldHandle(FieldDesc field) { throw new NotImplementedException(); }
        public IMethodNode FatFunctionPointer(MethodDesc method, bool isUnboxingStub = false) { return MethodEntrypoint(method); }
        public ISymbolNode TypeThreadStaticIndex(MetadataType type) { throw new NotImplementedException(); }

        public TargetDetails Target
        {
            get
            {
                return _targetDetails;
            }
        }

        public TypeSystemContext TypeSystemContext
        {
            get
            {
                return _typeSystemContext;
            }
        }

        public NameMangler NameMangler
        {
            get
            {
                return null; // No name mangling in the jit
            }
        }

        public ISymbolNode ReadyToRunHelperFromDictionaryLookup(ReadyToRunHelperId id, Object target, TypeSystemEntity dictionaryOwner)
        {
            throw new NotImplementedException();
        }

        public ISymbolNode ReadyToRunHelperFromTypeLookup(ReadyToRunHelperId id, Object target, TypeSystemEntity dictionaryOwner)
        {
            throw new NotImplementedException();
        }

        public DependencyNode VirtualMethodUse(MethodDesc decl)
        {
            throw new NotImplementedException();
        }

        internal DependencyNodeCore<NodeFactory> VTable(TypeDesc type)
        {
            throw new NotImplementedException();
        }

        public ISymbolNode TypeNonGCStaticsSymbol(MetadataType type)
        {
            throw new NotImplementedException();
        }

        public ISymbolNode TypeGCStaticsSymbol(MetadataType type)
        {
            throw new NotImplementedException();
        }

        private static readonly string[][] s_helperEntrypointNames = new string[][] {
            new string[] { "System.Runtime.CompilerServices", "ClassConstructorRunner", "CheckStaticClassConstructionReturnGCStaticBase" },
            new string[] { "System.Runtime.CompilerServices", "ClassConstructorRunner", "CheckStaticClassConstructionReturnNonGCStaticBase" },
            new string[] { "System.Runtime.CompilerServices", "ClassConstructorRunner", "CheckStaticClassConstructionReturnThreadStaticBase" },
            new string[] { "Internal.Runtime", "ThreadStatics", "GetThreadStaticBaseForType" }
        };

        private ISymbolNode[] _helperEntrypointSymbols;

        public ISymbolNode HelperEntrypoint(HelperEntrypoint entrypoint)
        {
            if (_helperEntrypointSymbols == null)
                _helperEntrypointSymbols = new ISymbolNode[s_helperEntrypointNames.Length];

            int index = (int)entrypoint;

            ISymbolNode symbol = _helperEntrypointSymbols[index];
            if (symbol == null)
            {
                var entry = s_helperEntrypointNames[index];

                var type = TypeSystemContext.SystemModule.GetKnownType(entry[0], entry[1]);
                var method = type.GetKnownMethod(entry[2], null);

                symbol = MethodEntrypoint(method);

                _helperEntrypointSymbols[index] = symbol;
            }
            return symbol;
        }
    }
}
