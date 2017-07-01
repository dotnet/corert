// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents the layout of the generic dictionary associated with a given canonical
    /// generic type or generic method. Maintains a bag of <see cref="GenericLookupResult"/> associated
    /// with the canonical entity.
    /// </summary>
    /// <remarks>
    /// The generic dictionary doesn't have any dependent nodes because <see cref="GenericLookupResult"/>
    /// are runtime-determined - the concrete dependency depends on the generic context the canonical
    /// entity is instantiated with.
    /// </remarks>
    public abstract class DictionaryLayoutNode : DependencyNodeCore<NodeFactory>
    {
        private readonly TypeSystemEntity _owningMethodOrType;

        public DictionaryLayoutNode(TypeSystemEntity owningMethodOrType)
        {
            _owningMethodOrType = owningMethodOrType;
            Validate();
        }

        [Conditional("DEBUG")]
        private void Validate()
        {
            TypeDesc type = _owningMethodOrType as TypeDesc;
            if (type != null)
            {
                Debug.Assert(type.IsCanonicalSubtype(CanonicalFormKind.Any));
                Debug.Assert(type.IsDefType);
            }
            else
            {
                MethodDesc method = _owningMethodOrType as MethodDesc;
                Debug.Assert(method != null && method.IsSharedByGenericInstantiations);
            }
        }

        public abstract int GetSlotForEntry(GenericLookupResult entry);

        public abstract IEnumerable<GenericLookupResult> Entries
        {
            get;
        }

        public abstract ICollection<NativeLayoutVertexNode> GetTemplateEntries(NodeFactory factory);

        public abstract void EmitDictionaryData(ref ObjectDataBuilder builder, NodeFactory factory, GenericDictionaryNode dictionary);

        protected override string GetName(NodeFactory factory) => $"Dictionary layout for {_owningMethodOrType.ToString()}";

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
    }

    public sealed class LazilyBuiltDictionaryLayoutNode : DictionaryLayoutNode
    {
        class EntryHashTable : LockFreeReaderHashtable<GenericLookupResult, GenericLookupResult>
        {
            protected override bool CompareKeyToValue(GenericLookupResult key, GenericLookupResult value) => Object.Equals(key, value);
            protected override bool CompareValueToValue(GenericLookupResult value1, GenericLookupResult value2) => Object.Equals(value1, value2);
            protected override GenericLookupResult CreateValueFromKey(GenericLookupResult key) => key;
            protected override int GetKeyHashCode(GenericLookupResult key) => key.GetHashCode();
            protected override int GetValueHashCode(GenericLookupResult value) => value.GetHashCode();
        }

        private EntryHashTable _entries = new EntryHashTable();
        private volatile GenericLookupResult[] _layout;

        public LazilyBuiltDictionaryLayoutNode(TypeSystemEntity owningMethodOrType)
            : base(owningMethodOrType)
        {
        }

        public void EnsureEntry(GenericLookupResult entry)
        {
            Debug.Assert(_layout == null, "Trying to add entry but layout already computed");
            _entries.AddOrGetExisting(entry);
        }

        private void ComputeLayout()
        {
            GenericLookupResult[] layout = new GenericLookupResult[_entries.Count];
            int index = 0;
            foreach (GenericLookupResult entry in EntryHashTable.Enumerator.Get(_entries))
            {
                layout[index++] = entry;
            }

            var comparer = new GenericLookupResult.Comparer(new TypeSystemComparer());
            Array.Sort(layout, comparer.Compare);

            // Only publish after the full layout is computed. Races are fine.
            _layout = layout;
        }

        public override int GetSlotForEntry(GenericLookupResult entry)
        {
            if (_layout == null)
                ComputeLayout();

            int index = Array.IndexOf(_layout, entry);
            Debug.Assert(index >= 0);
            return index;
        }

        public override IEnumerable<GenericLookupResult> Entries
        {
            get
            {
                if (_layout == null)
                    ComputeLayout();

                return _layout;
            }
        }

        public override ICollection<NativeLayoutVertexNode> GetTemplateEntries(NodeFactory factory)
        {
            if (_layout == null)
                ComputeLayout();

            ArrayBuilder<NativeLayoutVertexNode> templateEntries = new ArrayBuilder<NativeLayoutVertexNode>();
            for (int i = 0; i < _layout.Length; i++)
            {
                templateEntries.Add(_layout[i].TemplateDictionaryNode(factory));
            }

            return templateEntries.ToArray();
        }

        public override void EmitDictionaryData(ref ObjectDataBuilder builder, NodeFactory factory, GenericDictionaryNode dictionary)
        {
            var context = new GenericLookupResultContext(dictionary.OwningEntity, dictionary.TypeInstantiation, dictionary.MethodInstantiation);

            foreach (GenericLookupResult lookupResult in Entries)
            {
#if DEBUG
                int offsetBefore = builder.CountBytes;
#endif

                lookupResult.EmitDictionaryEntry(ref builder, factory, context);

#if DEBUG
                Debug.Assert(builder.CountBytes - offsetBefore == factory.Target.PointerSize);
#endif
            }
        }
    }
}
