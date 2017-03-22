// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
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
    class UtcDictionaryLayoutNode : DictionaryLayoutNode
    {
        private List<GenericLookupResult> _layout = new List<GenericLookupResult>();
        private Dictionary<GenericLookupResult, int> _entryslots = new Dictionary<GenericLookupResult, int>();
        private Object thisLock = new Object();

        public UtcDictionaryLayoutNode(TypeSystemEntity owningMethodOrType) : base(owningMethodOrType)
        {
        }

        public override int GetSlotForEntry(GenericLookupResult entry)
        {
            int index;

            lock (thisLock)
            {
                if (!_entryslots.TryGetValue(entry, out index))
                {
                    _layout.Add(entry);
                    index = _layout.Count - 1;
                    _entryslots.Add(entry, index);
                }
            }           

            Debug.Assert(index >= 0);
            return index;
        }

        public override ICollection<GenericLookupResult> Entries
        {
            get
            {
                return _layout;
            }
        }
    }
}
