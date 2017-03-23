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

        protected override string GetName(NodeFactory factory) => $"__vtable_{factory.NameMangler.GetMangledTypeName(_type).ToString()}";

        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;

        protected IEnumerable<MethodDesc> GetAllVirtualMethods()
        {
            foreach (MethodDesc method in _type.GetAllMethods())
            {
                if (method.IsVirtual)
                    yield return method;
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

            bool isObjectType = type.IsObject;
            DefType defType = _type.GetClosestDefType();

            IEnumerable<MethodDesc> allSlots = _type.IsInterface ?
                GetAllVirtualMethods() : defType.EnumAllVirtualSlots();

            foreach (var method in allSlots)
            {
                // GVMs are not emitted in the type's vtable.
                if (method.HasInstantiation)
                    continue;

                // Finalizers are called via a field on the EEType, not through the VTable
                if (isObjectType && method.Name == "Finalize")
                    continue;

                // Current type doesn't define this slot.
                if (method.OwningType != defType)
                    continue;

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
        private HashSet<MethodDesc> _usedMethods = new HashSet<MethodDesc>();
        private MethodDesc[] _slots;

        public LazilyBuiltVTableSliceNode(TypeDesc type)
            : base(type)
        {
        }

        public override IReadOnlyList<MethodDesc> Slots
        {
            get
            {
                if (_slots == null)
                {
                    // Sort the lazily populated slots in metadata order (the order in which they show up
                    // in GetAllMethods()).
                    // This ensures that Foo<string> and Foo<object> will end up with the same vtable
                    // no matter the order in which VirtualMethodUse nodes populated it.
                    ArrayBuilder<MethodDesc> slotsBuilder = new ArrayBuilder<MethodDesc>();
                    DefType defType = _type.GetClosestDefType();
                    foreach (var method in defType.GetAllMethods())
                    {
                        if (_usedMethods.Contains(method))
                            slotsBuilder.Add(method);
                    }
                    Debug.Assert(_usedMethods.Count == slotsBuilder.Count);
                    _slots = slotsBuilder.ToArray();

                    // Null out used methods so that we AV if someone tries to add now.
                    _usedMethods = null;
                }

                return _slots;
            }
        }

        public void AddEntry(NodeFactory factory, MethodDesc virtualMethod)
        {
            // GVMs are not emitted in the type's vtable.
            Debug.Assert(!virtualMethod.HasInstantiation);
            Debug.Assert(virtualMethod.IsVirtual);
            Debug.Assert(_slots == null && _usedMethods != null);

            _usedMethods.Add(virtualMethod);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            if (_type.HasBaseType)
            {
                return new[] { new DependencyListEntry(factory.VTable(_type.BaseType), "Base type VTable") };
            }

            return null;
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                return _type.ConvertToCanonForm(CanonicalFormKind.Specific) != _type;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            // VirtualMethodUse of Foo<SomeType>.Method will bring in VirtualMethodUse
            // of Foo<__Canon>.Method. This in turn should bring in Foo<OtherType>.Method.
            DefType defType = _type.GetClosestDefType();

            IEnumerable<MethodDesc> allSlots = _type.IsInterface ?
                GetAllVirtualMethods() : defType.EnumAllVirtualSlots();

            foreach (var method in allSlots)
            {
                // Generic virtual methods are tracked by an orthogonal mechanism.
                if (method.HasInstantiation)
                    continue;

                // Current type doesn't define this slot. Another VTableSlice will take care of this.
                if (method.OwningType != defType)
                    continue;

                if (defType.Context.SupportsCanon)
                    yield return new CombinedDependencyListEntry(
                        factory.VirtualMethodUse(method),
                        factory.VirtualMethodUse(method.GetCanonMethodTarget(CanonicalFormKind.Specific)),
                        "Canonically equivalent virtual method use");

                if (defType.Context.SupportsUniversalCanon)
                    yield return new CombinedDependencyListEntry(
                        factory.VirtualMethodUse(method),
                        factory.VirtualMethodUse(method.GetCanonMethodTarget(CanonicalFormKind.Universal)),
                        "Universal Canonically equivalent virtual method use");
            }
        }
    }
}
