// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// Part of Node factory that deals with nodes describing results of generic lookups.
    /// See: <see cref="GenericLookupResult"/>.
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
                _typeSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeHandleGenericLookupResult(type);
                });

                _methodDictionaries = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new MethodDictionaryGenericLookupResult(method);
                });

                _methodEntrypoints = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new MethodEntryGenericLookupResult(method);
                });

                _virtualCallHelpers = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new VirtualDispatchGenericLookupResult(method);
                });
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeSymbols;

            public GenericLookupResult Type(TypeDesc type)
            {
                return _typeSymbols.GetOrAdd(type);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _methodDictionaries;

            public GenericLookupResult MethodDictionary(MethodDesc method)
            {
                return _methodDictionaries.GetOrAdd(method);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _virtualCallHelpers;

            public GenericLookupResult VirtualCall(MethodDesc method)
            {
                return _virtualCallHelpers.GetOrAdd(method);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _methodEntrypoints;

            public GenericLookupResult MethodEntry(MethodDesc method)
            {
                return _methodEntrypoints.GetOrAdd(method);
            }
        }

        public GenericLookupResults GenericLookup = new GenericLookupResults();
    }
}
