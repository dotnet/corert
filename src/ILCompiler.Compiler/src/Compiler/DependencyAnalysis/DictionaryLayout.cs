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
        private object _owner;
        private HashSet<DictionaryEntry> _entries = new HashSet<DictionaryEntry>();

#if DEBUG
        private bool _slotsCommited;
#endif

        public DictionaryLayout(TypeDesc type)
        {
            Debug.Assert(type.HasInstantiation);
            Debug.Assert(type.IsRuntimeDeterminedSubtype);
            _owner = type;
        }

        public DictionaryLayout(MethodDesc method)
        {
            Debug.Assert(method.HasInstantiation);
            // TODO: assert the method is runtime determined
            _owner = method;
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(
            List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(
            NodeFactory context) => null;

        public override string GetName()
        {
            return String.Concat("Dictionary layout for " + _owner.ToString());
        }

        public void AddEntry(DictionaryEntry entry)
        {
#if DEBUG
            Debug.Assert(!_slotsCommited);
#endif
            _entries.Add(entry);
        }

        public IEnumerable<DictionaryEntry> Entries
        {
            get
            {
#if DEBUG
                _slotsCommited = true;
#endif

                // TODO: deterministic ordering
                return _entries;
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
    }
}
