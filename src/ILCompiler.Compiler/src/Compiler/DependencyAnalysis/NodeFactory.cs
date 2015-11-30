// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    public class NodeFactory
    {
        private TargetDetails _target;
        private CompilerTypeSystemContext _context;

        public NodeFactory(CompilerTypeSystemContext context)
        {
            _target = context.Target;
            _context = context;
            CreateNodeCaches();
        }

        public TargetDetails Target
        {
            get
            {
                return _target;
            }
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
            _typeSymbols = new NodeCache<TypeDesc, EETypeNode>((TypeDesc type) =>
            {
                return new EETypeNode(type, false);
            });

            _constructedTypeSymbols = new NodeCache<TypeDesc, EETypeNode>((TypeDesc type) =>
            {
                return new EETypeNode(type, true);
            });


            _nonGCStatics = new NodeCache<MetadataType, NonGCStaticsNode>((MetadataType type) =>
            {
                return new NonGCStaticsNode(type);
            });

            _GCStatics = new NodeCache<MetadataType, GCStaticsNode>((MetadataType type) =>
            {
                return new GCStaticsNode(type, this);
            });

            _threadStatics = new NodeCache<MetadataType, ThreadStaticsNode>((MetadataType type) =>
            {
                return new ThreadStaticsNode(type, this);
            });

            _GCStaticEETypes = new NodeCache<bool[], GCStaticEETypeNode>((bool[] gcdesc) =>
            {
                return new GCStaticEETypeNode(gcdesc, this);
            }, new BoolArrayEqualityComparer());

            _readOnlyDataBlobs = new NodeCache<Tuple<string, byte[], int>, BlobNode>((Tuple<string, byte[], int> key) =>
            {
                return new BlobNode(key.Item1, "text", key.Item2, key.Item3);
            });

            _externSymbols = new NodeCache<string, ExternSymbolNode>((string name) =>
            {
                return new ExternSymbolNode(name);
            });

            _internalSymbols = new NodeCache<Tuple<ObjectNode, int, string>, ObjectAndOffsetSymbolNode>(
                (Tuple<ObjectNode, int, string> key) =>
                {
                    return new ObjectAndOffsetSymbolNode(key.Item1, key.Item2, key.Item3);
                });

            _methodCode = new NodeCache<MethodDesc, MethodCodeNode>((MethodDesc method) =>
            {
                return new MethodCodeNode(method);
            });

            _jumpStubs = new NodeCache<ISymbolNode, JumpStubNode>((ISymbolNode node) =>
            {
                return new JumpStubNode(node);
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
        }

        private NodeCache<TypeDesc, EETypeNode> _typeSymbols;

        public ISymbolNode NecessaryTypeSymbol(TypeDesc type)
        {
            return _typeSymbols.GetOrAdd(type);
        }

        private NodeCache<TypeDesc, EETypeNode> _constructedTypeSymbols;

        public ISymbolNode ConstructedTypeSymbol(TypeDesc type)
        {
            return _constructedTypeSymbols.GetOrAdd(type);
        }

        private NodeCache<MetadataType, NonGCStaticsNode> _nonGCStatics;

        public ISymbolNode TypeNonGCStaticsSymbol(MetadataType type)
        {
            return _nonGCStatics.GetOrAdd(type);
        }

        public ISymbolNode TypeCctorContextSymbol(MetadataType type)
        {
            return _nonGCStatics.GetOrAdd(type).ClassConstructorContext;
        }

        private NodeCache<MetadataType, GCStaticsNode> _GCStatics;

        public GCStaticsNode TypeGCStaticsSymbol(MetadataType type)
        {
            return _GCStatics.GetOrAdd(type);
        }

        private NodeCache<MetadataType, ThreadStaticsNode> _threadStatics;

        public ThreadStaticsNode TypeThreadStaticsSymbol(MetadataType type)
        {
            return _threadStatics.GetOrAdd(type);
        }

        private class BoolArrayEqualityComparer : IEqualityComparer<bool[]>
        {
            bool IEqualityComparer<bool[]>.Equals(bool[] x, bool[] y)
            {
                if (x.Length != y.Length)
                    return false;

                for (int i = 0; i < x.Length; i++)
                {
                    if (x[i] != y[i])
                        return false;
                }

                return true;
            }

            int IEqualityComparer<bool[]>.GetHashCode(bool[] obj)
            {
                // TODO get better combining function for bools
                int hash = 0x5d83481;
                foreach (bool b in obj)
                {
                    int bAsInt = b ? 1 : 0;
                    hash = (hash << 4) ^ hash ^ bAsInt;
                }

                return hash;
            }
        }

        private NodeCache<bool[], GCStaticEETypeNode> _GCStaticEETypes;

        public ISymbolNode GCStaticEEType(bool[] gcdesc)
        {
            return _GCStaticEETypes.GetOrAdd(gcdesc);
        }

        private NodeCache<Tuple<string, byte[], int>, BlobNode> _readOnlyDataBlobs;

        public BlobNode ReadOnlyDataBlob(string name, byte[] blobData, int alignment)
        {
            return _readOnlyDataBlobs.GetOrAdd(new Tuple<string, byte[], int>(name, blobData, alignment));
        }

        private NodeCache<string, ExternSymbolNode> _externSymbols;

        public ISymbolNode ExternSymbol(string name)
        {
            return _externSymbols.GetOrAdd(name);
        }

        private NodeCache<Tuple<ObjectNode, int, string>, ObjectAndOffsetSymbolNode> _internalSymbols;

        public ISymbolNode ObjectAndOffset(ObjectNode obj, int offset, string name)
        {
            return _internalSymbols.GetOrAdd(new Tuple<ObjectNode, int, string>(obj, offset, name));
        }

        private NodeCache<MethodDesc, MethodCodeNode> _methodCode;
        private NodeCache<ISymbolNode, JumpStubNode> _jumpStubs;

        public ISymbolNode MethodEntrypoint(MethodDesc method)
        {
            var kind = method.DetectSpecialMethodKind();
            if (kind == SpecialMethodKind.PInvoke)
            {
                return _jumpStubs.GetOrAdd(ExternSymbol(method.GetPInvokeMethodMetadata().Name));
            }
            else if (kind == SpecialMethodKind.RuntimeImport)
            {
                return ExternSymbol(((EcmaMethod)method).GetAttributeStringValue("System.Runtime", "RuntimeImportAttribute"));
            }

            return _methodCode.GetOrAdd(method);
        }

        private static readonly string[][] s_helperEntrypointNames = new string[][] {
            new string[] { "System.Runtime.CompilerServices", "CctorHelper", "CheckStaticClassConstructionReturnGCStaticBase" },
            new string[] { "System.Runtime.CompilerServices", "CctorHelper", "CheckStaticClassConstructionReturnNonGCStaticBase" }
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

                var type = _context.SystemModule.GetType(entry[0], entry[1]);
                var method = type.GetMethod(entry[2], null);

                symbol = MethodEntrypoint(method);

                _helperEntrypointSymbols[index] = symbol;
            }
            return symbol;
        }

        private NodeCache<MethodDesc, VirtualMethodUseNode> _virtMethods;

        public DependencyNode VirtualMethodUse(MethodDesc decl)
        {
            return _virtMethods.GetOrAdd(decl);
        }

        private NodeCache<Tuple<ReadyToRunHelperId, Object>, ReadyToRunHelperNode> _readyToRunHelpers;

        public ReadyToRunHelperNode ReadyToRunHelper(ReadyToRunHelperId id, Object target)
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

        public ArrayOfEmbeddedDataNode GCStaticsRegion = new ArrayOfEmbeddedDataNode(
            NameMangler.CompilationUnitPrefix + "__GCStaticRegionStart", 
            NameMangler.CompilationUnitPrefix + "__GCStaticRegionEnd", 
            null);
        public ArrayOfEmbeddedDataNode ThreadStaticsRegion = new ArrayOfEmbeddedDataNode(
            NameMangler.CompilationUnitPrefix + "__ThreadStaticRegionStart",
            NameMangler.CompilationUnitPrefix + "__ThreadStaticRegionEnd", 
            null);
        public ArrayOfEmbeddedDataNode StringTable = new ArrayOfEmbeddedDataNode(
            NameMangler.CompilationUnitPrefix + "__str_fixup",
            NameMangler.CompilationUnitPrefix + "__str_fixup_end", 
            null);

        public Dictionary<TypeDesc, List<MethodDesc>> VirtualSlots = new Dictionary<TypeDesc, List<MethodDesc>>();

        public Dictionary<ISymbolNode, string> NodeAliases = new Dictionary<ISymbolNode, string>();

        public static NameMangler NameMangler;

        public void AttachToDependencyGraph(DependencyAnalysisFramework.DependencyAnalyzerBase<NodeFactory> graph)
        {
            graph.AddRoot(GCStaticsRegion, "GC StaticsRegion is always generated");
            graph.AddRoot(ThreadStaticsRegion, "ThreadStaticsRegion is always generated");
            graph.AddRoot(StringTable, "StringTable is always generated");
        }
    }

    public enum HelperEntrypoint
    {
        EnsureClassConstructorRunAndReturnGCStaticBase,
        EnsureClassConstructorRunAndReturnNonGCStaticBase,
    }
}
