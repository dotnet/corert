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
    public class GCStaticDescNode : EmbeddedObjectNode, ISymbolNode
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

        protected override string GetName() => this.GetMangledName();

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
        
        protected override void OnMarked(NodeFactory factory)
        {
            UtcNodeFactory hostedFactory = factory as UtcNodeFactory;
            Debug.Assert(hostedFactory != null);
            hostedFactory.GCStaticDescRegion.AddEmbeddedObject(this);
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
            UtcNodeFactory hostedFactory = factory as UtcNodeFactory;
            Debug.Assert(hostedFactory != null);
            DependencyListEntry[] result = new DependencyListEntry[2];           
            result[0] = new DependencyListEntry(hostedFactory.GCStaticDescRegion, "GCStaticDesc Region");
            result[1] = new DependencyListEntry(hostedFactory.TypeGCStaticsSymbol(_type), "GC Static Base Symbol");
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
                    // The cell ends the current series
                    builder.EmitInt(gcFieldCount);

                    if (_isThreadStatic)
                    {
                        builder.EmitReloc((factory as UtcNodeFactory).TlsStart, RelocType.IMAGE_REL_SECREL, startIndex * factory.Target.PointerSize);
                    }
                    else
                    {
                        builder.EmitReloc(factory.TypeGCStaticsSymbol(_type), RelocType.IMAGE_REL_BASED_RELPTR32, startIndex * factory.Target.PointerSize);
                    }

                    gcFieldCount = 0;
                    numSeries++;
                }
            }

            Debug.Assert(numSeries == NumSeries);
        }
    }

    public class GCStaticDescRegionNode : ArrayOfEmbeddedDataNode<GCStaticDescNode>
    {
        public GCStaticDescRegionNode(string startSymbolMangledName, string endSymbolMangledName)
            : base(startSymbolMangledName, endSymbolMangledName, null)
        {
        }

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
                    node.Offset = builder.CountBytes;

                node.EncodeData(ref builder, factory, relocsOnly);
            }
        }
    }
}
