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
    /// generic type or generic method. Maintains a bag of <see cref="DictionaryEntry"/> associated
    /// with the canonical entity.
    /// </summary>
    /// <remarks>
    /// The generic dictionary doesn't have any dependent nodes because <see cref="DictionaryEntry"/>
    /// are runtime-determined - the concrete dependency depends on the generic context the canonical
    /// entity is instantiated with.
    /// </remarks>
    class DictionaryLayoutNode : DependencyNodeCore<NodeFactory>
    {
        class EntryHashTable : LockFreeReaderHashtable<DictionaryEntry, DictionaryEntry>
        {
            protected override bool CompareKeyToValue(DictionaryEntry key, DictionaryEntry value) => Object.Equals(key, value);
            protected override bool CompareValueToValue(DictionaryEntry value1, DictionaryEntry value2) => Object.Equals(value1, value2);
            protected override DictionaryEntry CreateValueFromKey(DictionaryEntry key) => key;
            protected override int GetKeyHashCode(DictionaryEntry key) => key.GetHashCode();
            protected override int GetValueHashCode(DictionaryEntry value) => value.GetHashCode();
        }

        private TypeSystemEntity _owningMethodOrType;
        private EntryHashTable _entries = new EntryHashTable();
        private volatile DictionaryEntry[] _layout;

        public DictionaryLayoutNode(TypeSystemEntity owningMethodOrType)
        {
            _owningMethodOrType = owningMethodOrType;
            Validate();
        }

        public void EnsureEntry(DictionaryEntry entry)
        {
            Debug.Assert(_layout == null, "Trying to add entry but layout already computed");
            _entries.AddOrGetExisting(entry);
        }

        private void ComputeLayout()
        {
            // TODO: deterministic ordering
            DictionaryEntry[] layout = new DictionaryEntry[_entries.Count];
            int index = 0;
            foreach (DictionaryEntry entry in EntryHashTable.Enumerator.Get(_entries))
            {
                layout[index++] = entry;
            }

            // Only publish after the full layout is computed. Races are fine.
            _layout = layout;
        }

        public int GetSlotForEntry(DictionaryEntry entry)
        {
            if (_layout == null)
                ComputeLayout();

            int index = Array.IndexOf(_layout, entry);
            Debug.Assert(index >= 0);
            return index;
        }

        public IEnumerable<DictionaryEntry> Entries
        {
            get
            {
                if (_layout == null)
                    ComputeLayout();

                return _layout;
            }
        }

        [Conditional("DEBUG")]
        private void Validate()
        {
            TypeDesc type = _owningMethodOrType as TypeDesc;
            if (type != null)
            {
                Debug.Assert(type.IsCanonicalSubtype(CanonicalFormKind.Any));
            }
            else
            {
                MethodDesc method = _owningMethodOrType as MethodDesc;
                Debug.Assert(method != null && method.IsCanonicalMethod(CanonicalFormKind.Any));
            }
        }

        protected override string GetName()
        {
            return String.Concat("Dictionary layout for " + _owningMethodOrType.ToString());
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            foreach (DictionaryEntry entry in EntryHashTable.Enumerator.Get(_entries))
                yield return new DependencyListEntry(entry, "Canonical dependency");
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;
    }
}
