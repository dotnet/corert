// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Runtime;

namespace ILCompiler.DependencyAnalysis
{
    class ModuleHeaderItemNode : EmbeddedObjectNode, ISymbolNode
    {
        ModuleHeaderSection _dataItem;
        ISymbolNode _startNode;
        ISymbolNode _endNode;

        public ModuleHeaderItemNode(ModuleHeaderSection dataItem, ISymbolNode startNode, ISymbolNode endNode = null)
        {
            _dataItem = dataItem;
            _startNode = startNode;
            _endNode = endNode;
        }

        public string MangledName
        {
            get
            {
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__ModuleHeaderItem_" + Enum.GetName(typeof(ModuleHeaderSection), _dataItem);
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        private ModuleInfoFlags ComputeFlags()
        {
            ModuleInfoFlags flags = 0;

            if (_endNode != null)
            {
                flags |= ModuleInfoFlags.HasEndPointer;
            }

            return flags;
        }

        public override void EncodeData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.RequirePointerAlignment();
            builder.EmitInt((int)_dataItem);
            builder.EmitInt((int)ComputeFlags());
            builder.EmitPointerReloc(_startNode);

            if (_endNode != null)
            {
                builder.EmitPointerReloc(_endNode);
            }
            else
            {
                builder.EmitZeroPointer();
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            var dependencies = new DependencyList();

            dependencies.Add(new DependencyListEntry(_startNode, "ModuleHeaderItemNode"));

            if (_endNode != null)
            {
                dependencies.Add(new DependencyListEntry(_endNode, "ModuleHeaderItemNode"));
            }

            return dependencies;
        }
    }
}
