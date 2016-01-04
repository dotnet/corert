// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    public class InterfaceDispatchMapTableNode : ObjectNode, ISymbolNode
    {
        List<InterfaceDispatchMapNode> _dispatchMaps = new List<InterfaceDispatchMapNode>();
        
        public InterfaceDispatchMapTableNode()
        {
        }

        public string MangledName
        {
            get
            {
                return "__InterfaceDispatchMapTable";
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public int Offset
        {
            get
            {
                return 0;
            }
        }

        public override string Section
        {
            get
            {
                return "rdata";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        internal uint AddDispatchMap(InterfaceDispatchMapNode node)
        {
            _dispatchMaps.Add(node);

            return (uint)_dispatchMaps.Count - 1;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.Alignment = factory.Target.PointerSize;
            objData.DefinedSymbols.Add(this);
            
            foreach (var map in _dispatchMaps)
            {
                objData.EmitPointerReloc(map);
            }
            
            return objData.ToObjectData();
        }
    }
}