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
    class DictionaryLayoutNode : DependencyNodeCore<NodeFactory>
    {
        class EntryHashTable : LockFreeReaderHashtable<GenericLookupResult, GenericLookupResult>
        {
            protected override bool CompareKeyToValue(GenericLookupResult key, GenericLookupResult value) => Object.Equals(key, value);
            protected override bool CompareValueToValue(GenericLookupResult value1, GenericLookupResult value2) => Object.Equals(value1, value2);
            protected override GenericLookupResult CreateValueFromKey(GenericLookupResult key) => key;
            protected override int GetKeyHashCode(GenericLookupResult key) => key.GetHashCode();
            protected override int GetValueHashCode(GenericLookupResult value) => value.GetHashCode();
        }

        private TypeSystemEntity _owningMethodOrType;
        private EntryHashTable _entries = new EntryHashTable();
        private volatile GenericLookupResult[] _layout;

        public DictionaryLayoutNode(TypeSystemEntity owningMethodOrType)
        {
            _owningMethodOrType = owningMethodOrType;
            Validate();
        }

        public void EnsureEntry(GenericLookupResult entry)
        {
            Debug.Assert(_layout == null, "Trying to add entry but layout already computed");
            _entries.AddOrGetExisting(entry);
        }

        private void ComputeLayout()
        {
            // TODO: deterministic ordering
            GenericLookupResult[] layout = new GenericLookupResult[_entries.Count];
            int index = 0;
            foreach (GenericLookupResult entry in EntryHashTable.Enumerator.Get(_entries))
            {
                layout[index++] = entry;
            }

            // Only publish after the full layout is computed. Races are fine.
            _layout = layout;
        }

        public int GetSlotForEntry(GenericLookupResult entry)
        {
            if (_layout == null)
                ComputeLayout();

            int index = Array.IndexOf(_layout, entry);
            Debug.Assert(index >= 0);
            return index;
        }

        public IEnumerable<GenericLookupResult> Entries
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
                Debug.Assert(method != null && method.IsSharedByGenericInstantiations);
            }
        }

        protected override string GetName() => $"Dictionary layout for {_owningMethodOrType.ToString()}";

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
    }
}
