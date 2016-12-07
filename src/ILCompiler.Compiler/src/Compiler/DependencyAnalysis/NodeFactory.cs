// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;
using Internal.Runtime;
using Internal.IL;

namespace ILCompiler.DependencyAnalysis
{
    public abstract partial class NodeFactory
    {
        private TargetDetails _target;
        private CompilerTypeSystemContext _context;
        private CompilationModuleGroup _compilationModuleGroup;

        public NodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup)
        {
            _target = context.Target;
            _context = context;
            _compilationModuleGroup = compilationModuleGroup;
            CreateNodeCaches();

            MetadataManager = new MetadataGeneration(this);
        }

        public TargetDetails Target
        {
            get
            {
                return _target;
            }
        }

        public CompilationModuleGroup CompilationModuleGroup
        {
            get
            {
                return _compilationModuleGroup;
            }
        }

        public CompilerTypeSystemContext TypeSystemContext
        {
            get
            {
                return _context;
            }
        }

        public MetadataGeneration MetadataManager
        {
            get;
        }

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

        private void CreateNodeCaches()
        {
            _typeSymbols = new NodeCache<TypeDesc, IEETypeNode>((TypeDesc type) =>
            {
                if (_compilationModuleGroup.ContainsType(type))
                {
                    if (type.IsGenericDefinition)
                    {
                        return new GenericDefinitionEETypeNode(this, type);
                    }
                    else
                    {
                        return new EETypeNode(this, type);
                    }
                }
                else
                {
                    return new ExternEETypeSymbolNode(this, type);
                }
            });

            _constructedTypeSymbols = new NodeCache<TypeDesc, IEETypeNode>((TypeDesc type) =>
            {
                if (_compilationModuleGroup.ContainsType(type))
                {
                    return new ConstructedEETypeNode(this, type);
                }
                else
                {
                    return new ExternEETypeSymbolNode(this, type);
                }
            });

            _clonedTypeSymbols = new NodeCache<TypeDesc, IEETypeNode>((TypeDesc type) =>
            {
                // Only types that reside in other binaries should be cloned
                Debug.Assert(_compilationModuleGroup.ShouldReferenceThroughImportTable(type));
                return new ClonedConstructedEETypeNode(this, type);
            });

            _nonGCStatics = new NodeCache<MetadataType, NonGCStaticsNode>((MetadataType type) =>
            {
                return new NonGCStaticsNode(type, this);
            });

            _GCStatics = new NodeCache<MetadataType, GCStaticsNode>((MetadataType type) =>
            {
                return new GCStaticsNode(type);
            });

            _GCStaticIndirectionNodes = new NodeCache<MetadataType, EmbeddedObjectNode>((MetadataType type) =>
            {
                ISymbolNode gcStaticsNode = TypeGCStaticsSymbol(type);
                Debug.Assert(gcStaticsNode is GCStaticsNode);
                return GCStaticsRegion.NewNode((GCStaticsNode)gcStaticsNode);
            });

            _threadStatics = new NodeCache<MetadataType, ThreadStaticsNode>((MetadataType type) =>
            {
                return new ThreadStaticsNode(type, this);
            });

            _GCStaticEETypes = new NodeCache<GCPointerMap, GCStaticEETypeNode>((GCPointerMap gcMap) =>
            {
                return new GCStaticEETypeNode(Target, gcMap);
            });

            _readOnlyDataBlobs = new NodeCache<Tuple<Utf8String, byte[], int>, BlobNode>((Tuple<Utf8String, byte[], int> key) =>
            {
                return new BlobNode(key.Item1, ObjectNodeSection.ReadOnlyDataSection, key.Item2, key.Item3);
            }, new BlobTupleEqualityComparer());

            _externSymbols = new NodeCache<string, ExternSymbolNode>((string name) =>
            {
                return new ExternSymbolNode(name);
            });

            _pInvokeModuleFixups = new NodeCache<string, PInvokeModuleFixupNode>((string name) =>
            {
                return new PInvokeModuleFixupNode(name);
            });

            _pInvokeMethodFixups = new NodeCache<Tuple<string, string>, PInvokeMethodFixupNode>((Tuple<string, string> key) =>
            {
                return new PInvokeMethodFixupNode(key.Item1, key.Item2);
            });

            _methodEntrypoints = new NodeCache<MethodDesc, IMethodNode>(CreateMethodEntrypointNode);

            _unboxingStubs = new NodeCache<MethodDesc, IMethodNode>(CreateUnboxingStubNode);

            _fatFunctionPointers = new NodeCache<MethodDesc, FatFunctionPointerNode>(method =>
            {
                return new FatFunctionPointerNode(method);
            });

            _shadowConcreteMethods = new NodeCache<MethodDesc, IMethodNode>(method =>
            {
                return new ShadowConcreteMethodNode<MethodCodeNode>(method,
                    (MethodCodeNode)MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)));
            });

            _runtimeDeterminedMethods = new NodeCache<MethodDesc, IMethodNode>(method =>
            {
                return new RuntimeDeterminedMethodNode(method,
                    MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)));
            });

            _virtMethods = new NodeCache<MethodDesc, VirtualMethodUseNode>((MethodDesc method) =>
            {
                return new VirtualMethodUseNode(method);
            });

            _readyToRunHelpers = new NodeCache<Tuple<ReadyToRunHelperId, Object>, ISymbolNode>(CreateReadyToRunHelperNode);

            _genericReadyToRunHelpersFromDict = new NodeCache<Tuple<ReadyToRunHelperId, object, TypeSystemEntity>, ISymbolNode>(data =>
            {
                return new ReadyToRunGenericLookupFromDictionaryNode(this, data.Item1, data.Item2, data.Item3);
            });

            _genericReadyToRunHelpersFromType = new NodeCache<Tuple<ReadyToRunHelperId, object, TypeSystemEntity>, ISymbolNode>(data =>
            {
                return new ReadyToRunGenericLookupFromTypeNode(this, data.Item1, data.Item2, data.Item3);
            });

            _indirectionNodes = new NodeCache<ISymbolNode, IndirectionNode>(symbol =>
            {
                return new IndirectionNode(symbol);
            });

            _frozenStringNodes = new NodeCache<string, FrozenStringNode>((string data) =>
            {
                return new FrozenStringNode(data, Target);
            });

            _interfaceDispatchCells = new NodeCache<MethodDesc, InterfaceDispatchCellNode>((MethodDesc method) =>
            {
                return new InterfaceDispatchCellNode(method);
            });

            _interfaceDispatchMaps = new NodeCache<TypeDesc, InterfaceDispatchMapNode>((TypeDesc type) =>
            {
                return new InterfaceDispatchMapNode(type);
            });

            _interfaceDispatchMapIndirectionNodes = new NodeCache<TypeDesc, EmbeddedObjectNode>((TypeDesc type) =>
            {
                var dispatchMap = InterfaceDispatchMap(type);
                return DispatchMapTable.NewNodeWithSymbol(dispatchMap, (indirectionNode) =>
                {
                    dispatchMap.SetDispatchMapIndex(this, DispatchMapTable.IndexOfEmbeddedObject(indirectionNode));
                });
            });

            _genericCompositions = new NodeCache<GenericCompositionDetails, GenericCompositionNode>((GenericCompositionDetails details) =>
            {
                return new GenericCompositionNode(details);
            });

            _eagerCctorIndirectionNodes = new NodeCache<MethodDesc, EmbeddedObjectNode>((MethodDesc method) =>
            {
                Debug.Assert(method.IsStaticConstructor);
                Debug.Assert(TypeSystemContext.HasEagerStaticConstructor((MetadataType)method.OwningType));
                return EagerCctorTable.NewNode(MethodEntrypoint(method));
            });
            
            _vTableNodes = new NodeCache<TypeDesc, VTableSliceNode>((TypeDesc type ) =>
            {
                if (CompilationModuleGroup.ShouldProduceFullType(type))
                    return new EagerlyBuiltVTableSliceNode(type);
                else
                    return new LazilyBuiltVTableSliceNode(type);
            });

            _methodGenericDictionaries = new NodeCache<MethodDesc, GenericDictionaryNode>(method =>
            {
                return new MethodGenericDictionaryNode(method);
            });

            _typeGenericDictionaries = new NodeCache<TypeDesc, GenericDictionaryNode>(type =>
            {
                return new TypeGenericDictionaryNode(type);
            });

            _genericDictionaryLayouts = new NodeCache<TypeSystemEntity, DictionaryLayoutNode>(methodOrType =>
            {
                return new DictionaryLayoutNode(methodOrType);
            });

            _stringAllocators = new NodeCache<MethodDesc, IMethodNode>(constructor =>
            {
                return new StringAllocatorMethodNode(constructor);
            });
        }

        protected abstract IMethodNode CreateMethodEntrypointNode(MethodDesc method);

        protected abstract IMethodNode CreateUnboxingStubNode(MethodDesc method);

        protected abstract ISymbolNode CreateReadyToRunHelperNode(Tuple<ReadyToRunHelperId, Object> helperCall);

        private NodeCache<TypeDesc, IEETypeNode> _typeSymbols;

        public IEETypeNode NecessaryTypeSymbol(TypeDesc type)
        {
            return _typeSymbols.GetOrAdd(type);
        }

        private NodeCache<TypeDesc, IEETypeNode> _constructedTypeSymbols;

        public IEETypeNode ConstructedTypeSymbol(TypeDesc type)
        {
            return _constructedTypeSymbols.GetOrAdd(type);
        }

        private NodeCache<TypeDesc, IEETypeNode> _clonedTypeSymbols;

        public IEETypeNode ConstructedClonedTypeSymbol(TypeDesc type)
        {
            return _clonedTypeSymbols.GetOrAdd(type);
        }

        private NodeCache<MetadataType, NonGCStaticsNode> _nonGCStatics;

        public ISymbolNode TypeNonGCStaticsSymbol(MetadataType type)
        {
            if (_compilationModuleGroup.ContainsType(type))
            {
                return _nonGCStatics.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol("__NonGCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(type));
            }
        }
        
        private NodeCache<MetadataType, GCStaticsNode> _GCStatics;

        public ISymbolNode TypeGCStaticsSymbol(MetadataType type)
        {
            if (_compilationModuleGroup.ContainsType(type))
            {
                return _GCStatics.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol("__GCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(type));
            }
        }

        private NodeCache<MetadataType, EmbeddedObjectNode> _GCStaticIndirectionNodes;

        public EmbeddedObjectNode GCStaticIndirection(MetadataType type)
        {
            return _GCStaticIndirectionNodes.GetOrAdd(type);
        }

        private NodeCache<MetadataType, ThreadStaticsNode> _threadStatics;

        public ISymbolNode TypeThreadStaticsSymbol(MetadataType type)
        {
            if (_compilationModuleGroup.ContainsType(type))
            {
                return _threadStatics.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol("__ThreadStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName(type));
            }
        }

        private NodeCache<MethodDesc, InterfaceDispatchCellNode> _interfaceDispatchCells;

        internal InterfaceDispatchCellNode InterfaceDispatchCell(MethodDesc method)
        {
            return _interfaceDispatchCells.GetOrAdd(method);
        }

        private class BlobTupleEqualityComparer : IEqualityComparer<Tuple<Utf8String, byte[], int>>
        {
            bool IEqualityComparer<Tuple<Utf8String, byte[], int>>.Equals(Tuple<Utf8String, byte[], int> x, Tuple<Utf8String, byte[], int> y)
            {
                return x.Item1.Equals(y.Item1);
            }

            int IEqualityComparer<Tuple<Utf8String, byte[], int>>.GetHashCode(Tuple<Utf8String, byte[], int> obj)
            {
                return obj.Item1.GetHashCode();
            }
        }

        private NodeCache<GCPointerMap, GCStaticEETypeNode> _GCStaticEETypes;

        public ISymbolNode GCStaticEEType(GCPointerMap gcMap)
        {
            return _GCStaticEETypes.GetOrAdd(gcMap);
        }

        private NodeCache<Tuple<Utf8String, byte[], int>, BlobNode> _readOnlyDataBlobs;

        public BlobNode ReadOnlyDataBlob(Utf8String name, byte[] blobData, int alignment)
        {
            return _readOnlyDataBlobs.GetOrAdd(new Tuple<Utf8String, byte[], int>(name, blobData, alignment));
        }

        private NodeCache<TypeDesc, InterfaceDispatchMapNode> _interfaceDispatchMaps;

        internal InterfaceDispatchMapNode InterfaceDispatchMap(TypeDesc type)
        {
            return _interfaceDispatchMaps.GetOrAdd(type);
        }

        private NodeCache<TypeDesc, EmbeddedObjectNode> _interfaceDispatchMapIndirectionNodes;

        public EmbeddedObjectNode InterfaceDispatchMapIndirection(TypeDesc type)
        {
            return _interfaceDispatchMapIndirectionNodes.GetOrAdd(type);
        }

        private NodeCache<GenericCompositionDetails, GenericCompositionNode> _genericCompositions;

        internal ISymbolNode GenericComposition(GenericCompositionDetails details)
        {
            return _genericCompositions.GetOrAdd(details);
        }

        private NodeCache<string, ExternSymbolNode> _externSymbols;

        public ISymbolNode ExternSymbol(string name)
        {
            return _externSymbols.GetOrAdd(name);
        }

        private NodeCache<string, PInvokeModuleFixupNode> _pInvokeModuleFixups;

        public ISymbolNode PInvokeModuleFixup(string moduleName)
        {
            return _pInvokeModuleFixups.GetOrAdd(moduleName);
        }

        private NodeCache<Tuple<string, string>, PInvokeMethodFixupNode> _pInvokeMethodFixups;

        public PInvokeMethodFixupNode PInvokeMethodFixup(string moduleName, string entryPointName)
        {
            return _pInvokeMethodFixups.GetOrAdd(new Tuple<string, string>(moduleName, entryPointName));
        }

        private NodeCache<TypeDesc, VTableSliceNode> _vTableNodes;

        internal VTableSliceNode VTable(TypeDesc type)
        {
            return _vTableNodes.GetOrAdd(type);
        }

        private NodeCache<MethodDesc, GenericDictionaryNode> _methodGenericDictionaries;
        internal GenericDictionaryNode MethodGenericDictionary(MethodDesc method)
        {
            return _methodGenericDictionaries.GetOrAdd(method);
        }

        private NodeCache<TypeDesc, GenericDictionaryNode> _typeGenericDictionaries;
        internal GenericDictionaryNode TypeGenericDictionary(TypeDesc type)
        {
            return _typeGenericDictionaries.GetOrAdd(type);
        }

        private NodeCache<TypeSystemEntity, DictionaryLayoutNode> _genericDictionaryLayouts;
        internal DictionaryLayoutNode GenericDictionaryLayout(TypeSystemEntity methodOrType)
        {
            return _genericDictionaryLayouts.GetOrAdd(methodOrType);
        }

        private NodeCache<MethodDesc, IMethodNode> _stringAllocators;
        internal IMethodNode StringAllocator(MethodDesc stringConstructor)
        {
            return _stringAllocators.GetOrAdd(stringConstructor);
        }

        private NodeCache<MethodDesc, IMethodNode> _methodEntrypoints;
        private NodeCache<MethodDesc, IMethodNode> _unboxingStubs;

        public IMethodNode MethodEntrypoint(MethodDesc method, bool unboxingStub = false)
        {
            if (unboxingStub)
            {
                return _unboxingStubs.GetOrAdd(method);
            }

            return _methodEntrypoints.GetOrAdd(method);
        }

        private NodeCache<MethodDesc, FatFunctionPointerNode> _fatFunctionPointers;

        public IMethodNode FatFunctionPointer(MethodDesc method)
        {
            return _fatFunctionPointers.GetOrAdd(method);
        }

        private NodeCache<MethodDesc, IMethodNode> _shadowConcreteMethods;

        public IMethodNode ShadowConcreteMethod(MethodDesc method)
        {
            return _shadowConcreteMethods.GetOrAdd(method);
        }

        private NodeCache<MethodDesc, IMethodNode> _runtimeDeterminedMethods;

        public IMethodNode RuntimeDeterminedMethod(MethodDesc method)
        {
            return _runtimeDeterminedMethods.GetOrAdd(method);
        }

        private static readonly string[][] s_helperEntrypointNames = new string[][] {
            new string[] { "System.Runtime.CompilerServices", "ClassConstructorRunner", "CheckStaticClassConstructionReturnGCStaticBase" },
            new string[] { "System.Runtime.CompilerServices", "ClassConstructorRunner", "CheckStaticClassConstructionReturnNonGCStaticBase" }
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

                var type = _context.SystemModule.GetKnownType(entry[0], entry[1]);
                var method = type.GetKnownMethod(entry[2], null);

                symbol = MethodEntrypoint(method);

                _helperEntrypointSymbols[index] = symbol;
            }
            return symbol;
        }

        private MetadataType _systemArrayOfTClass;
        public MetadataType ArrayOfTClass
        {
            get
            {
                if (_systemArrayOfTClass == null)
                {
                    _systemArrayOfTClass = _context.SystemModule.GetKnownType("System", "Array`1");
                }
                return _systemArrayOfTClass;
            }
        }

        private TypeDesc _systemArrayOfTEnumeratorType;
        public TypeDesc ArrayOfTEnumeratorType
        {
            get
            {
                if (_systemArrayOfTEnumeratorType == null)
                {
                    _systemArrayOfTEnumeratorType = ArrayOfTClass.GetNestedType("ArrayEnumerator");
                }
                return _systemArrayOfTEnumeratorType;
            }
        }

        private TypeDesc _systemICastableType;

        public TypeDesc ICastableInterface
        {
            get
            {
                if (_systemICastableType == null)
                {
                    _systemICastableType = _context.SystemModule.GetKnownType("System.Runtime.CompilerServices", "ICastable");
                }
                return _systemICastableType;
            }
        }

        private NodeCache<MethodDesc, VirtualMethodUseNode> _virtMethods;

        public DependencyNode VirtualMethodUse(MethodDesc decl)
        {
            return _virtMethods.GetOrAdd(decl);
        }

        private NodeCache<Tuple<ReadyToRunHelperId, Object>, ISymbolNode> _readyToRunHelpers;

        public ISymbolNode ReadyToRunHelper(ReadyToRunHelperId id, Object target)
        {
            return _readyToRunHelpers.GetOrAdd(new Tuple<ReadyToRunHelperId, object>(id, target));
        }

        private NodeCache<Tuple<ReadyToRunHelperId, Object, TypeSystemEntity>, ISymbolNode> _genericReadyToRunHelpersFromDict;

        public ISymbolNode ReadyToRunHelperFromDictionaryLookup(ReadyToRunHelperId id, Object target, TypeSystemEntity dictionaryOwner)
        {
            return _genericReadyToRunHelpersFromDict.GetOrAdd(new Tuple<ReadyToRunHelperId, object, TypeSystemEntity>(id, target, dictionaryOwner));
        }

        private NodeCache<Tuple<ReadyToRunHelperId, Object, TypeSystemEntity>, ISymbolNode> _genericReadyToRunHelpersFromType;

        public ISymbolNode ReadyToRunHelperFromTypeLookup(ReadyToRunHelperId id, Object target, TypeSystemEntity dictionaryOwner)
        {
            return _genericReadyToRunHelpersFromType.GetOrAdd(new Tuple<ReadyToRunHelperId, object, TypeSystemEntity>(id, target, dictionaryOwner));
        }

        private NodeCache<ISymbolNode, IndirectionNode> _indirectionNodes;

        public IndirectionNode Indirection(ISymbolNode symbol)
        {
            return _indirectionNodes.GetOrAdd(symbol);
        }

        private NodeCache<string, FrozenStringNode> _frozenStringNodes;

        public FrozenStringNode SerializedStringObject(string data)
        {
            return _frozenStringNodes.GetOrAdd(data);
        }

        private NodeCache<MethodDesc, EmbeddedObjectNode> _eagerCctorIndirectionNodes;

        public EmbeddedObjectNode EagerCctorIndirection(MethodDesc cctorMethod)
        {
            return _eagerCctorIndirectionNodes.GetOrAdd(cctorMethod);
        }

        public ISymbolNode ConstantUtf8String(string str)
        {
            int stringBytesCount = Encoding.UTF8.GetByteCount(str);
            byte[] stringBytes = new byte[stringBytesCount + 1];
            Encoding.UTF8.GetBytes(str, 0, str.Length, stringBytes, 0);

            string symbolName = "__utf8str_" + NameMangler.GetMangledStringName(str);

            return ReadOnlyDataBlob(symbolName, stringBytes, 1);
        }

        public ISymbolNode ConstantUtf16String(string str)
        {
            int stringBytesCount = Encoding.Unicode.GetByteCount(str);
            byte[] stringBytes = new byte[stringBytesCount + 2];
            Encoding.Unicode.GetBytes(str, 0, str.Length, stringBytes, 0);

            string symbolName = "__utf16str_" + NameMangler.GetMangledStringName(str);

            return ReadOnlyDataBlob(symbolName, stringBytes, 2);
        }

        /// <summary>
        /// Returns alternative symbol name that object writer should produce for given symbols
        /// in addition to the regular one.
        /// </summary>
        public string GetSymbolAlternateName(ISymbolNode node)
        {
            string value;
            if (!NodeAliases.TryGetValue(node, out value))
                return null;
            return value;
        }

        public ArrayOfEmbeddedPointersNode<GCStaticsNode> GCStaticsRegion = new ArrayOfEmbeddedPointersNode<GCStaticsNode>(
            "__GCStaticRegionStart", 
            "__GCStaticRegionEnd", 
            null);
        public ArrayOfEmbeddedDataNode ThreadStaticsRegion = new ArrayOfEmbeddedDataNode(
            "__ThreadStaticRegionStart",
            "__ThreadStaticRegionEnd", 
            null);

        public ArrayOfEmbeddedPointersNode<IMethodNode> EagerCctorTable = new ArrayOfEmbeddedPointersNode<IMethodNode>(
            "__EagerCctorStart",
            "__EagerCctorEnd",
            new EagerConstructorComparer());

        public ArrayOfEmbeddedPointersNode<InterfaceDispatchMapNode> DispatchMapTable = new ArrayOfEmbeddedPointersNode<InterfaceDispatchMapNode>(
            "__DispatchMapTableStart",
            "__DispatchMapTableEnd",
            null);

        public ArrayOfEmbeddedDataNode<FrozenStringNode> FrozenSegmentRegion = new ArrayOfFrozenObjectsNode<FrozenStringNode>(
            "__FrozenSegmentRegionStart",
            "__FrozenSegmentRegionEnd",
            null);

        public ReadyToRunHeaderNode ReadyToRunHeader;

        public Dictionary<ISymbolNode, string> NodeAliases = new Dictionary<ISymbolNode, string>();

        internal TypeManagerIndirectionNode TypeManagerIndirection = new TypeManagerIndirectionNode();

        public static NameMangler NameMangler;

        public virtual void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            ReadyToRunHeader = new ReadyToRunHeaderNode(Target);

            graph.AddRoot(ReadyToRunHeader, "ReadyToRunHeader is always generated");
            graph.AddRoot(new ModulesSectionNode(Target), "ModulesSection is always generated");

            graph.AddRoot(GCStaticsRegion, "GC StaticsRegion is always generated");
            graph.AddRoot(ThreadStaticsRegion, "ThreadStaticsRegion is always generated");
            graph.AddRoot(EagerCctorTable, "EagerCctorTable is always generated");
            graph.AddRoot(TypeManagerIndirection, "TypeManagerIndirection is always generated");
            graph.AddRoot(DispatchMapTable, "DispatchMapTable is always generated");
            graph.AddRoot(FrozenSegmentRegion, "FrozenSegmentRegion is always generated");

            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticRegion, GCStaticsRegion, GCStaticsRegion.StartSymbol, GCStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticRegion, ThreadStaticsRegion, ThreadStaticsRegion.StartSymbol, ThreadStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.EagerCctor, EagerCctorTable, EagerCctorTable.StartSymbol, EagerCctorTable.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.TypeManagerIndirection, TypeManagerIndirection, TypeManagerIndirection);
            ReadyToRunHeader.Add(ReadyToRunSectionType.InterfaceDispatchTable, DispatchMapTable, DispatchMapTable.StartSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.FrozenObjectRegion, FrozenSegmentRegion, FrozenSegmentRegion.StartSymbol, FrozenSegmentRegion.EndSymbol);

            MetadataManager.AddToReadyToRunHeader(ReadyToRunHeader);
            MetadataManager.AttachToDependencyGraph(graph);
        }
    }

    public enum HelperEntrypoint
    {
        EnsureClassConstructorRunAndReturnGCStaticBase,
        EnsureClassConstructorRunAndReturnNonGCStaticBase,
    }
}
