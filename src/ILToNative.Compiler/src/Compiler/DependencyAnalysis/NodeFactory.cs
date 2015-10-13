// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using ILToNative.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILToNative.DependencyAnalysis
{
    public class NodeFactory
    {
        TargetDetails _target;

        public NodeFactory(TargetDetails target)
        {
            _target = target;
            CreateNodeCaches();
        }

        public TargetDetails Target
        {
            get
            {
                return _target;
            }
        }

        struct NodeCache<TKey, TValue>
        {
            Func<TKey, TValue> _creator;
            Dictionary<TKey, TValue> _cache;

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

            _GCStaticEETypes = new NodeCache<bool[], GCStaticEETypeNode>((bool[] gcdesc) =>
            {
                return new GCStaticEETypeNode(gcdesc, this);
            }, new BoolArrayEqualityComparer());

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

            _virtMethods = new NodeCache<MethodDesc, VirtualMethodUseNode>((MethodDesc method) =>
            {
                return new VirtualMethodUseNode(method);
            });

            _readyToRunHelpers = new NodeCache<ReadyToRunHelper, ReadyToRunHelperNode>((ReadyToRunHelper helper) =>
            {
                return new ReadyToRunHelperNode(helper);
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

        private NodeCache<MetadataType, GCStaticsNode> _GCStatics;

        public GCStaticsNode TypeGCStaticsSymbol(MetadataType type)
        {
            return _GCStatics.GetOrAdd(type);
        }

        class BoolArrayEqualityComparer : IEqualityComparer<bool[]>
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

        public ISymbolNode MethodEntrypoint(MethodDesc method)
        {
            return _methodCode.GetOrAdd(method);
        }

        private NodeCache<MethodDesc, VirtualMethodUseNode> _virtMethods;

        public DependencyNode VirtualMethodUse(MethodDesc decl)
        {
            return _virtMethods.GetOrAdd(decl);
        }

        private NodeCache<ReadyToRunHelper, ReadyToRunHelperNode> _readyToRunHelpers;

        public ReadyToRunHelperNode ReadyToRunHelper(ReadyToRunHelper helper)
        {
            return _readyToRunHelpers.GetOrAdd(helper);
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

        public ArrayOfEmbeddedDataNode GCStaticsRegion = new ArrayOfEmbeddedDataNode("__GCStaticRegionStart", "__GCStaticRegionEnd", null);
        public ArrayOfEmbeddedDataNode StringTable = new ArrayOfEmbeddedDataNode("__str_fixup", "__str_fixup_end", null);

        public Dictionary<TypeDesc, List<MethodDesc>> VirtualSlots = new Dictionary<TypeDesc, List<MethodDesc>>();

        public static NameMangler NameMangler;

        public void AttachToDependencyGraph(DependencyAnalysisFramework.DependencyAnalyzerBase<NodeFactory> graph)
        {
            graph.AddRoot(GCStaticsRegion, "GC StaticsRegion is always generated");
            graph.AddRoot(StringTable, "StringTable is always generated");
        }
    }
}
