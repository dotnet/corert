// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the VTable for a type's slice. For example, System.String's VTableSliceNode includes virtual 
    /// slots added by System.String itself, System.Object's EETypeVTableSliceNode contains the virtuals it defines.
    /// </summary>
    internal class VTableSliceNode : DependencyNodeCore<NodeFactory>
    {
        TypeDesc _type;
        bool _shouldBuildFullVTable;
        List<MethodDesc> _slots = new List<MethodDesc>();

        public VTableSliceNode(TypeDesc type, NodeFactory factory)
        {
            _type = type;
            if (factory.CompilationModuleGroup.ShouldProduceFullType(_type))
            {
                _shouldBuildFullVTable = true;
            }
        }
        
        public IReadOnlyList<MethodDesc> Slots
        {
            get
            {
                return _slots;
            }
        }

        public void AddEntry(NodeFactory factory, MethodDesc virtualMethod)
        {
            Debug.Assert(virtualMethod.IsVirtual);

            if (_shouldBuildFullVTable)
                return;
            
            if (!_slots.Contains(virtualMethod))
            {
                _slots.Add(virtualMethod);
            }
        }
        
        public override string GetName()
        {
            return "__vtable_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            List<DependencyListEntry> dependencies = new List<DependencyListEntry>();
            if (_type.HasBaseType)
            {
                dependencies.Add(new DependencyListEntry(factory.VTable(_type.BaseType), "Base type VTable"));
            }

            if (_shouldBuildFullVTable)
            {
                Debug.Assert(_slots.Count == 0);

                // When building a full VTable (such as for a type from a library), add dependencies on
                // the virtual methods so that methods overriding them will be marked in the graph
                // and compiled.
                MetadataType mdType = _type.GetClosestMetadataType();
                foreach (var method in mdType.GetMethods())
                {
                    if (!method.IsVirtual)
                        continue;

                    dependencies.Add(new DependencyListEntry(factory.VirtualMethodUse(method), "VTable method dependency"));
                    _slots.Add(method);
                }
            }

            return dependencies;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
        {
            return null;
        }
        
        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                return false;
            }
        }

        public override bool HasDynamicDependencies
        {
            get
            {
                return false;
            }
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                return false;
            }
        }
    }
}
