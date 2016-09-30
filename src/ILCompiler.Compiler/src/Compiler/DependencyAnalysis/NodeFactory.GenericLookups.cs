// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// Part of Node factory that deals with nodes describing results of generic lookups.
    /// See: <see cref="GenericLookupResultNode"/>.
    partial class NodeFactory
    {
        /// <summary>
        /// Helper class that provides a level of grouping for all the generic lookup result kinds.
        /// </summary>
        public class GenericLookupResults
        {
            public GenericLookupResults()
            {
                CreateNodeCaches();
            }

            private void CreateNodeCaches()
            {
                _typeSymbols = new NodeCache<TypeDesc, GenericLookupResultNode>(type =>
                {
                    return new TypeHandleDictionaryEntry(type);
                });
            }

            private NodeCache<TypeDesc, GenericLookupResultNode> _typeSymbols;

            public GenericLookupResultNode Type(TypeDesc type)
            {
                return _typeSymbols.GetOrAdd(type);
            }
        }

        public GenericLookupResults GenericLookup = new GenericLookupResults();
    }
}
