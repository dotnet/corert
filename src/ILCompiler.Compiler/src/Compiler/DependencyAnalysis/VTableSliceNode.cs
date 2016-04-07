// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the VTable for a type's slice. For example, System.String's VTableSliceNode includes virtual 
    /// slots added by System.String itself, System.Object's VTableSliceNode contains the virtuals it defines.
    /// </summary>
    internal abstract class VTableSliceNode : DependencyNodeCore<NodeFactory>
    {
        protected TypeDesc _type;

        public VTableSliceNode(TypeDesc type)
        {
            _type = type;
        }
        
        public abstract IReadOnlyList<MethodDesc> Slots
        {
            get;
        }
        
        public override string GetName()
        {
            return "__vtable_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
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

    /// <summary>
    /// Represents a VTable slice for a complete type - a type with all virtual method slots generated,
    /// irrespective of whether they are used.
    /// </summary>
    internal sealed class EagerlyBuiltVTableSliceNode : VTableSliceNode
    {
        private MethodDesc[] _slots;

        public EagerlyBuiltVTableSliceNode(TypeDesc type)
            : base(type)
        {
            var slots = new ArrayBuilder<MethodDesc>();

            MetadataType mdType = _type.GetClosestMetadataType();
            foreach (var method in mdType.GetAllVirtualMethods())
            {
                slots.Add(method);
            }

            _slots = slots.ToArray();
        }

        public override IReadOnlyList<MethodDesc> Slots
        {
            get
            {
                return _slots;
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            List<DependencyListEntry> dependencies = new List<DependencyListEntry>(_slots.Length + 1);
            if (_type.HasBaseType)
            {
                dependencies.Add(new DependencyListEntry(factory.VTable(_type.BaseType), "Base type VTable"));
            }

            foreach (MethodDesc method in _slots)
            {
                dependencies.Add(new DependencyListEntry(factory.VirtualMethodUse(method), "Full vtable dependency"));
            }

            return dependencies;
        }
    }

    /// <summary>
    /// Represents a VTable slice where slots are built on demand. Only the slots that are actually used
    /// will be generated.
    /// </summary>
    internal sealed class LazilyBuiltVTableSliceNode : VTableSliceNode
    {
        private List<MethodDesc> _slots = new List<MethodDesc>();

#if DEBUG
        bool _slotsCommitted;
#endif

        public LazilyBuiltVTableSliceNode(TypeDesc type)
            : base(type)
        {
        }

        public override IReadOnlyList<MethodDesc> Slots
        {
            get
            {
#if DEBUG
                _slotsCommitted = true;
#endif
                return _slots;
            }
        }

        public void AddEntry(NodeFactory factory, MethodDesc virtualMethod)
        {
            Debug.Assert(virtualMethod.IsVirtual);
#if DEBUG
            Debug.Assert(!_slotsCommitted);
#endif

            if (!_slots.Contains(virtualMethod))
            {
                _slots.Add(virtualMethod);
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            if (_type.HasBaseType)
            {
                return new[] { new DependencyListEntry(factory.VTable(_type.BaseType), "Base type VTable") };
            }

            return null;
        }
    }
}
