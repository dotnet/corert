// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class GCStaticDescNode : EmbeddedObjectNode, ISymbolDefinitionNode, ISortableSymbolNode
    {
        private MetadataType _type;
        private GCPointerMap _gcMap;
        private bool _isThreadStatic;

        public GCStaticDescNode(MetadataType type, bool isThreadStatic)
        {
            _type = type;
            _gcMap = isThreadStatic ? GCPointerMap.FromThreadStaticLayout(type) : GCPointerMap.FromStaticLayout(type);
            _isThreadStatic = isThreadStatic;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetMangledName(nameMangler, _type, _isThreadStatic));
        }

        public static string GetMangledName(NameMangler nameMangler, MetadataType type, bool isThreadStatic)
        {
            string prefix = isThreadStatic ? "__ThreadStaticGCDesc_" : "__GCStaticDesc_";
            return prefix + nameMangler.GetMangledTypeName(type);
        }

        public int NumSeries
        {
            get
            {
                return _gcMap.NumSeries;                
            }
        }

        int ISymbolNode.Offset => 0;

        int ISymbolDefinitionNode.Offset => OffsetFromBeginningOfArray;
        
        private GCStaticDescRegionNode Region(NodeFactory factory)
        {
            UtcNodeFactory utcNodeFactory = (UtcNodeFactory)factory;

            if (_type.IsCanonicalSubtype(CanonicalFormKind.Any))
            {
                return null;
            }
            else
            {
                if (_isThreadStatic)
                {
                    return utcNodeFactory.ThreadStaticGCDescRegion;
                }
                else
                {
                    return utcNodeFactory.GCStaticDescRegion;
                }
            }
        }

        private ISymbolNode GCStaticsSymbol(NodeFactory factory)
        {
            UtcNodeFactory utcNodeFactory = (UtcNodeFactory)factory;

            if (_isThreadStatic)
            {
                return utcNodeFactory.TypeThreadStaticsSymbol(_type);
            }
            else
            {
                return utcNodeFactory.TypeGCStaticsSymbol(_type);
            }
        }

        protected override void OnMarked(NodeFactory factory)
        {
            GCStaticDescRegionNode region = Region(factory);
            if (region != null)
                region.AddEmbeddedObject(this);
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyListEntry[] result;
            if (!_type.IsCanonicalSubtype(CanonicalFormKind.Any))
            {
                result = new DependencyListEntry[2];
                result[0] = new DependencyListEntry(Region(factory), "GCStaticDesc Region");
                result[1] = new DependencyListEntry(GCStaticsSymbol(factory), "GC Static Base Symbol");
            }
            else
            {
                Debug.Assert(Region(factory) == null);
                result = new DependencyListEntry[1];
                result[0] = new DependencyListEntry(((UtcNodeFactory)factory).StandaloneGCStaticDescRegion(this), "Standalone GCStaticDesc holder");
            }

            return result;
        }

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            int gcFieldCount = 0;
            int startIndex = 0;
            int numSeries = 0;

            for (int i = 0; i < _gcMap.Size; i++)
            {
                // Skip non-GC fields
                if (!_gcMap[i])
                    continue;

                gcFieldCount++;

                if (i == 0 || !_gcMap[i - 1])
                {
                    // The cell starts a new series
                    startIndex = i;
                }

                if (i == _gcMap.Size - 1 || !_gcMap[i + 1])
                {
                    if (_type.IsCanonicalSubtype(CanonicalFormKind.Any))
                    {
                        // The cell ends the current series
                        builder.EmitInt(gcFieldCount * factory.Target.PointerSize);
                        builder.EmitInt(startIndex * factory.Target.PointerSize);
                    }
                    else
                    {
                        // The cell ends the current series
                        builder.EmitInt(gcFieldCount);

                        if (_isThreadStatic)
                        {
                            builder.EmitReloc(factory.TypeThreadStaticsSymbol(_type), RelocType.IMAGE_REL_SECREL, startIndex * factory.Target.PointerSize);
                        }
                        else
                        {
                            builder.EmitReloc(factory.TypeGCStaticsSymbol(_type), RelocType.IMAGE_REL_BASED_RELPTR32, startIndex * factory.Target.PointerSize);
                        }
                    }

                    gcFieldCount = 0;
                    numSeries++;
                }
            }

            Debug.Assert(numSeries == NumSeries);
        }

        internal int CompareTo(GCStaticDescNode other, TypeSystemComparer comparer)
        {
            var compare = _isThreadStatic.CompareTo(other._isThreadStatic);
            return compare != 0 ? compare : comparer.Compare(_type, other._type);
        }

        public sealed override int ClassCode => 2142332918;
        
        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return CompareTo((GCStaticDescNode)other, comparer);
        }
    }

    public class GCStaticDescRegionNode : ArrayOfEmbeddedDataNode<GCStaticDescNode>
    {
        public GCStaticDescRegionNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<GCStaticDescNode> nodeSorter)
            : base(startSymbolMangledName, endSymbolMangledName, nodeSorter)
        {
        }

        public override int ClassCode => 1312891560;

        protected override void GetElementDataForNodes(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            int numSeries = 0;
            foreach (GCStaticDescNode descNode in NodesList)
            {
                numSeries += descNode.NumSeries;
            }

            builder.EmitInt(numSeries);

            foreach (GCStaticDescNode node in NodesList)
            {
                if (!relocsOnly)
                    node.InitializeOffsetFromBeginningOfArray(builder.CountBytes);

                node.EncodeData(ref builder, factory, relocsOnly);
                builder.AddSymbol(node);
            }
        }
    }

    public class StandaloneGCStaticDescRegionNode : ObjectNode
    {
        GCStaticDescNode _standaloneGCStaticDesc;

        public StandaloneGCStaticDescRegionNode(GCStaticDescNode standaloneGCStaticDesc)
        {
            _standaloneGCStaticDesc = standaloneGCStaticDesc;
        }

        public override ObjectNodeSection Section => ObjectNodeSection.ReadOnlyDataSection;

        public override bool IsShareable => true;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();

            builder.AddSymbol(_standaloneGCStaticDesc);
            _standaloneGCStaticDesc.InitializeOffsetFromBeginningOfArray(0);
            builder.EmitInt(_standaloneGCStaticDesc.NumSeries);
            _standaloneGCStaticDesc.EncodeData(ref builder, factory, relocsOnly);

            return builder.ToObjectData();
        }

        protected override string GetName(NodeFactory context)
        {
            return "Standalone" + _standaloneGCStaticDesc.GetMangledName(context.NameMangler);
        }
        
        public override int ClassCode => 2091208431;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _standaloneGCStaticDesc.CompareTo(((StandaloneGCStaticDescRegionNode)other)._standaloneGCStaticDesc, comparer);
        }
    }
}
