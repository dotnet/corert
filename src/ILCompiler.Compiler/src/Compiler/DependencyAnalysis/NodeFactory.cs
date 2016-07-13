// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;
using Internal.Runtime;
using Internal.IL;

namespace ILCompiler.DependencyAnalysis
{
    public class NodeFactory
    {
        private TargetDetails _target;
        private CompilerTypeSystemContext _context;
        private bool _cppCodeGen;
        private CompilationModuleGroup _compilationModuleGroup;

        public NodeFactory(CompilerTypeSystemContext context, TypeInitialization typeInitManager, CompilationModuleGroup compilationModuleGroup, bool cppCodeGen)
        {
            _target = context.Target;
            _context = context;
            _cppCodeGen = cppCodeGen;
            _compilationModuleGroup = compilationModuleGroup;
            TypeInitializationManager = typeInitManager;
            CreateNodeCaches();

            MetadataManager = new MetadataGeneration();
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

        public TypeInitialization TypeInitializationManager
        {
            get; private set;
        }

        public MetadataGeneration MetadataManager
        {
            get; private set;
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
                    return new EETypeNode(type, false);
                }
                else
                {
                    return new ExternEETypeSymbolNode(type);
                }
            });

            _constructedTypeSymbols = new NodeCache<TypeDesc, IEETypeNode>((TypeDesc type) =>
            {
                if (_compilationModuleGroup.ContainsType(type))
                {
                    return new EETypeNode(type, true);
                }
                else
                {
                    return new ExternEETypeSymbolNode(type);
                }
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

            _readOnlyDataBlobs = new NodeCache<Tuple<string, byte[], int>, BlobNode>((Tuple<string, byte[], int> key) =>
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

            _internalSymbols = new NodeCache<Tuple<ObjectNode, int, string>, ObjectAndOffsetSymbolNode>(
                (Tuple<ObjectNode, int, string> key) =>
                {
                    return new ObjectAndOffsetSymbolNode(key.Item1, key.Item2, key.Item3);
                });

            _methodEntrypoints = new NodeCache<MethodDesc, IMethodNode>((MethodDesc method) =>
            {
                if (!_cppCodeGen)
                {
                    if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
                    {
                        return new RuntimeImportMethodNode(method);
                    }
                }

                if (_compilationModuleGroup.ContainsMethod(method))
                {
                    if (_cppCodeGen)
                        return new CppMethodCodeNode(method);
                    else
                        return new MethodCodeNode(method);
                }
                else
                {
                    return new ExternMethodSymbolNode(method);
                }
            });

            _unboxingStubs = new NodeCache<MethodDesc, IMethodNode>((MethodDesc method) =>
            {
                return new UnboxingStubNode(method);
            });

            _virtMethods = new NodeCache<MethodDesc, VirtualMethodUseNode>((MethodDesc method) =>
            {
                return new VirtualMethodUseNode(method);
            });

            _readyToRunHelpers = new NodeCache<Tuple<ReadyToRunHelperId, Object>, ReadyToRunHelperNode>((Tuple < ReadyToRunHelperId, Object > helper) =>
            {
                return new ReadyToRunHelperNode(helper.Item1, helper.Item2);
            });

            _stringDataNodes = new NodeCache<string, StringDataNode>((string data) =>
            {
                return new StringDataNode(data);
            });

            _stringIndirectionNodes = new NodeCache<string, StringIndirectionNode>((string data) =>
            {
                return new StringIndirectionNode(data);
            });

            _typeOptionalFields = new NodeCache<EETypeOptionalFieldsBuilder, EETypeOptionalFieldsNode>((EETypeOptionalFieldsBuilder fieldBuilder) =>
            {
                return new EETypeOptionalFieldsNode(fieldBuilder, this.Target);
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
                Debug.Assert(TypeInitializationManager.HasEagerStaticConstructor((MetadataType)method.OwningType));
                return EagerCctorTable.NewNode(MethodEntrypoint(method));
            });
            
            _vTableNodes = new NodeCache<TypeDesc, VTableSliceNode>((TypeDesc type ) =>
            {
                if (CompilationModuleGroup.ShouldProduceFullType(type))
                    return new EagerlyBuiltVTableSliceNode(type);
                else
                    return new LazilyBuiltVTableSliceNode(type);
            });

            _jumpThunks = new NodeCache<Tuple<ExternSymbolNode, ISymbolNode>, SingleArgumentJumpThunk>((Tuple<ExternSymbolNode, ISymbolNode> data) =>
            {
                return new SingleArgumentJumpThunk(data.Item1, data.Item2);
            });
        }

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

        private class BlobTupleEqualityComparer : IEqualityComparer<Tuple<string, byte[], int>>
        {
            bool IEqualityComparer<Tuple<string, byte[], int>>.Equals(Tuple<string, byte[], int> x, Tuple<string, byte[], int> y)
            {
                return x.Item1.Equals(y.Item1);
            }

            int IEqualityComparer<Tuple<string, byte[], int>>.GetHashCode(Tuple<string, byte[], int> obj)
            {
                return obj.Item1.GetHashCode();
            }
        }

        private NodeCache<GCPointerMap, GCStaticEETypeNode> _GCStaticEETypes;

        public ISymbolNode GCStaticEEType(GCPointerMap gcMap)
        {
            return _GCStaticEETypes.GetOrAdd(gcMap);
        }

        private NodeCache<Tuple<string, byte[], int>, BlobNode> _readOnlyDataBlobs;

        public BlobNode ReadOnlyDataBlob(string name, byte[] blobData, int alignment)
        {
            return _readOnlyDataBlobs.GetOrAdd(new Tuple<string, byte[], int>(name, blobData, alignment));
        }

        private NodeCache<EETypeOptionalFieldsBuilder, EETypeOptionalFieldsNode> _typeOptionalFields;

        internal EETypeOptionalFieldsNode EETypeOptionalFields(EETypeOptionalFieldsBuilder fieldBuilder)
        {
            return _typeOptionalFields.GetOrAdd(fieldBuilder);
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

        private NodeCache<Tuple<ObjectNode, int, string>, ObjectAndOffsetSymbolNode> _internalSymbols;

        public ISymbolNode ObjectAndOffset(ObjectNode obj, int offset, string name)
        {
            return _internalSymbols.GetOrAdd(new Tuple<ObjectNode, int, string>(obj, offset, name));
        }

        private NodeCache<TypeDesc, VTableSliceNode> _vTableNodes;

        internal VTableSliceNode VTable(TypeDesc type)
        {
            return _vTableNodes.GetOrAdd(type);
        }

        private NodeCache<Tuple<ExternSymbolNode, ISymbolNode>, SingleArgumentJumpThunk> _jumpThunks;
        
        /// <summary>
        /// Create a thunk that calls an externally defined (e.g., native) function, passing
        /// a dependency node to the function it calls.
        /// </summary>
        internal SingleArgumentJumpThunk JumpThunk(ExternSymbolNode target, ISymbolNode argument)
        {
            return _jumpThunks.GetOrAdd(new Tuple<ExternSymbolNode, ISymbolNode>(target, argument));
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

        private NodeCache<Tuple<ReadyToRunHelperId, Object>, ReadyToRunHelperNode> _readyToRunHelpers;

        public ISymbolNode ReadyToRunHelper(ReadyToRunHelperId id, Object target)
        {
            return _readyToRunHelpers.GetOrAdd(new Tuple<ReadyToRunHelperId, object>(id, target));
        }

        private NodeCache<string, StringDataNode> _stringDataNodes;

        public StringDataNode StringData(string data)
        {
            return _stringDataNodes.GetOrAdd(data);
        }

        private NodeCache<string, StringIndirectionNode> _stringIndirectionNodes;

        public StringIndirectionNode StringIndirection(string data)
        {
            return _stringIndirectionNodes.GetOrAdd(data);
        }

        private NodeCache<MethodDesc, EmbeddedObjectNode> _eagerCctorIndirectionNodes;

        public EmbeddedObjectNode EagerCctorIndirection(MethodDesc cctorMethod)
        {
            return _eagerCctorIndirectionNodes.GetOrAdd(cctorMethod);
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
            NameMangler.CompilationUnitPrefix + "__GCStaticRegionStart", 
            NameMangler.CompilationUnitPrefix + "__GCStaticRegionEnd", 
            null);
        public ArrayOfEmbeddedDataNode ThreadStaticsRegion = new ArrayOfEmbeddedDataNode(
            NameMangler.CompilationUnitPrefix + "__ThreadStaticRegionStart",
            NameMangler.CompilationUnitPrefix + "__ThreadStaticRegionEnd", 
            null);
        public ArrayOfEmbeddedDataNode StringTable = new ArrayOfEmbeddedDataNode(
            NameMangler.CompilationUnitPrefix + "__StringTableStart",
            NameMangler.CompilationUnitPrefix + "__StringTableEnd", 
            null);

        public ArrayOfEmbeddedPointersNode<IMethodNode> EagerCctorTable = new ArrayOfEmbeddedPointersNode<IMethodNode>(
            NameMangler.CompilationUnitPrefix + "__EagerCctorStart",
            NameMangler.CompilationUnitPrefix + "__EagerCctorEnd",
            new EagerConstructorComparer());

        public ArrayOfEmbeddedPointersNode<InterfaceDispatchMapNode> DispatchMapTable = new ArrayOfEmbeddedPointersNode<InterfaceDispatchMapNode>(
            NameMangler.CompilationUnitPrefix + "__DispatchMapTableStart",
            NameMangler.CompilationUnitPrefix + "__DispatchMapTableEnd",
            null);

        public ReadyToRunHeaderNode ReadyToRunHeader;

        public Dictionary<ISymbolNode, string> NodeAliases = new Dictionary<ISymbolNode, string>();

        internal ModuleManagerIndirectionNode ModuleManagerIndirection = new ModuleManagerIndirectionNode();

        public static NameMangler NameMangler;

        public void AttachToDependencyGraph(DependencyAnalysisFramework.DependencyAnalyzerBase<NodeFactory> graph)
        {
            ReadyToRunHeader = new ReadyToRunHeaderNode(Target);

            graph.AddRoot(ReadyToRunHeader, "ReadyToRunHeader is always generated");
            graph.AddRoot(new ModulesSectionNode(Target), "ModulesSection is always generated");

            graph.AddRoot(GCStaticsRegion, "GC StaticsRegion is always generated");
            graph.AddRoot(ThreadStaticsRegion, "ThreadStaticsRegion is always generated");
            graph.AddRoot(StringTable, "StringTable is always generated");
            graph.AddRoot(EagerCctorTable, "EagerCctorTable is always generated");
            graph.AddRoot(ModuleManagerIndirection, "ModuleManagerIndirection is always generated");
            graph.AddRoot(DispatchMapTable, "DispatchMapTable is always generated");

            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticRegion, GCStaticsRegion, GCStaticsRegion.StartSymbol, GCStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticRegion, ThreadStaticsRegion, ThreadStaticsRegion.StartSymbol, ThreadStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.StringTable, StringTable, StringTable.StartSymbol, StringTable.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.EagerCctor, EagerCctorTable, EagerCctorTable.StartSymbol, EagerCctorTable.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ModuleManagerIndirection, ModuleManagerIndirection, ModuleManagerIndirection);
            ReadyToRunHeader.Add(ReadyToRunSectionType.InterfaceDispatchTable, DispatchMapTable, DispatchMapTable.StartSymbol);

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
