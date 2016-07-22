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
    class DictionaryLayout : DependencyNodeCore<NodeFactory>
    {
        private object _owningMethodOrType;
        private HashSet<DictionaryEntry> _entries = new HashSet<DictionaryEntry>();
        private DictionaryEntry[] _layout;

#if DEBUG
        private bool _slotsCommited;
#endif

        public DictionaryLayout(object owningMethodOrType)
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
            }
            else
            {
                MethodDesc method = (MethodDesc)_owningMethodOrType;
                Debug.Assert(method.IsCanonicalMethod(CanonicalFormKind.Any));
            }
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory factory) => null;

        public override string GetName()
        {
            return String.Concat("Dictionary layout for " + _owningMethodOrType.ToString());
        }

        public void EnsureEntry(DictionaryEntry entry)
        {
#if DEBUG
            Debug.Assert(!_slotsCommited);
#endif

            // If the entry is the same for all instantiations, why are we putting it in a dictionary?
            Debug.Assert(entry.IsRuntimeDetermined);

            _entries.Add(entry);
        }

        private void ComputeLayout()
        {
#if DEBUG
            _slotsCommited = true;
#endif
            
            // TODO: deterministic ordering

            DictionaryEntry[] layout = new DictionaryEntry[_entries.Count];
            int index = 0;
            foreach (DictionaryEntry entry in _entries)
            {
                layout[index++] = entry;
            }

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
    }

    /// <summary>
    /// Represents a single entry in a <see cref="DictionaryLayout"/>.
    /// </summary>
    public struct DictionaryEntry : IEquatable<DictionaryEntry>
    {
        public readonly object Target;
        public readonly ReadyToRunFixupKind FixupKind;

        public DictionaryEntry(ReadyToRunFixupKind fixupKind, object target)
        {
            FixupKind = fixupKind;
            Target = target;
            Validate();
        }

        public bool IsRuntimeDetermined
        {
            get
            {
                TypeDesc targetType = Target as TypeDesc;
                if (targetType != null)
                {
                    return targetType.IsRuntimeDeterminedSubtype;
                }
                else
                    throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj)
        {
            return obj is DictionaryEntry && Equals((DictionaryEntry)obj);
        }

        public override int GetHashCode()
        {
            return Target.GetHashCode() ^ FixupKind.GetHashCode();
        }

        public bool Equals(DictionaryEntry other)
        {
            return this.Target == other.Target && this.FixupKind == other.FixupKind;
        }

        [Conditional("DEBUG")]
        private void Validate()
        {
            TypeDesc targetType = Target as TypeDesc;
            if (targetType != null)
            {
                // Target can be concrete or runtime determined, but never canonical
                Debug.Assert(!targetType.IsCanonicalSubtype(CanonicalFormKind.Any));
            }
            else
            {
                throw new NotImplementedException();
            }

            // TODO: validate that the particular fixup is valid for the target
        }

        public override string ToString()
        {
            return String.Concat(FixupKind.ToString(), ": ", Target.ToString());
        }
    }
}
