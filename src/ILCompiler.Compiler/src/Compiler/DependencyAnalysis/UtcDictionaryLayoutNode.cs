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
    public partial class UtcDictionaryLayoutNode : DictionaryLayoutNode
    {
        public override bool HasFixedSlots
        {
            get
            {
                return false;
            }
        }

        public UtcDictionaryLayoutNode(TypeSystemEntity owningMethodOrType) : base(owningMethodOrType)
        {

        }

#if CORERT
        public override void EnsureEntry(GenericLookupResult lookupResult) => throw new NotImplementedException();
        public override int GetSlotForEntry(GenericLookupResult entry) => throw new NotImplementedException();
        public override IEnumerable<GenericLookupResult> Entries => throw new NotImplementedException();
        public override ICollection<NativeLayoutVertexNode> GetTemplateEntries(NodeFactory factory) => throw new NotImplementedException();
        public override void EmitDictionaryData(ref ObjectDataBuilder builder, NodeFactory factory, GenericDictionaryNode dictionary, bool fixedLayoutOnly) => throw new NotImplementedException();
#endif
    }
}
