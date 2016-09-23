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
        private TypeSystemEntity _owningMethodOrType;
        private HashSet<DictionaryEntry> _entries = new HashSet<DictionaryEntry>();
        private DictionaryEntry[] _layout;

        public DictionaryLayoutNode(TypeSystemEntity owningMethodOrType)
        {
            _owningMethodOrType = owningMethodOrType;
            Validate();
        }

        public void EnsureEntry(DictionaryEntry entry)
        {
            // TODO: thread safety
            Debug.Assert(_layout == null, "Trying to add entry but layout already computed");

            _entries.Add(entry);
        }

        private void ComputeLayout()
        {
            HashSet<DictionaryEntry> entries = _entries;
            _entries = null;

            // TODO: deterministic ordering
            DictionaryEntry[] layout = new DictionaryEntry[entries.Count];
            int index = 0;
            foreach (DictionaryEntry entry in entries)
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

        public override string GetName()
        {
            return String.Concat("Dictionary layout for " + _owningMethodOrType.ToString());
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
    }

    /// <summary>
    /// Represents a single entry in a <see cref="DictionaryLayoutNode"/>.
    /// </summary>
    public abstract class DictionaryEntry
    {
        public abstract override bool Equals(object obj);
        public abstract override int GetHashCode();
        public abstract override string ToString();

        public abstract ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation);
        public abstract string GetMangledName(NameMangler nameMangler);

        protected static bool SameType<T>(T thisEntry, object other)
        {
            Debug.Assert(thisEntry.GetType() == typeof(T));
            return other != null && typeof(T) == other.GetType();
        }
    }

    public class TypeHandleDictionaryEntry : DictionaryEntry
    {
        private TypeDesc _type;

        public TypeHandleDictionaryEntry(TypeDesc type)
        {
            Debug.Assert(type.IsRuntimeDeterminedSubtype, "Concrete type in a generic dictionary?");
            _type = type;
        }

        public override ISymbolNode GetTarget(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc instantiatedType = _type.InstantiateSignature(typeInstantiation, methodInstantiation);
            return factory.NecessaryTypeSymbol(instantiatedType);
        }

        public override string GetMangledName(NameMangler nameMangler)
        {
            return $"TypeHandle_{nameMangler.GetMangledTypeName(_type)}";
        }

        public override bool Equals(object obj) => SameType(this, obj) && ((TypeHandleDictionaryEntry)obj)._type == _type;
        public override int GetHashCode() => _type.GetHashCode();
        public override string ToString() => $"TypeHandle: {_type}";
    }

}
