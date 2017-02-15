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

                _methodHandles = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new MethodHandleGenericLookupResult(method);
                });

                _fieldHandles = new NodeCache<FieldDesc, GenericLookupResult>(field =>
                {
                    return new FieldHandleGenericLookupResult(field);
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

                _virtualResolveHelpers = new NodeCache<MethodDesc, GenericLookupResult>(method =>
                {
                    return new VirtualResolveGenericLookupResult(method);
                });

                _typeThreadStaticBaseIndexSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeThreadStaticBaseIndexGenericLookupResult(type);
                });

                _typeGCStaticBaseSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeGCStaticBaseGenericLookupResult(type);
                });

                _typeNonGCStaticBaseSymbols = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new TypeNonGCStaticBaseGenericLookupResult(type);
                });

                _objectAllocators = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new ObjectAllocatorGenericLookupResult(type);
                });

                _arrayAllocators = new NodeCache<TypeDesc, GenericLookupResult>(type =>
                {
                    return new ArrayAllocatorGenericLookupResult(type);
                });
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeSymbols;

            public GenericLookupResult Type(TypeDesc type)
            {
                return _typeSymbols.GetOrAdd(type);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _methodHandles;

            public GenericLookupResult MethodHandle(MethodDesc method)
            {
                return _methodHandles.GetOrAdd(method);
            }

            private NodeCache<FieldDesc, GenericLookupResult> _fieldHandles;

            public GenericLookupResult FieldHandle(FieldDesc field)
            {
                return _fieldHandles.GetOrAdd(field);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeThreadStaticBaseIndexSymbols;

            public GenericLookupResult TypeThreadStaticBaseIndex(TypeDesc type)
            {
                return _typeThreadStaticBaseIndexSymbols.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeGCStaticBaseSymbols;

            public GenericLookupResult TypeGCStaticBase(TypeDesc type)
            {
                return _typeGCStaticBaseSymbols.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _typeNonGCStaticBaseSymbols;

            public GenericLookupResult TypeNonGCStaticBase(TypeDesc type)
            {
                return _typeNonGCStaticBaseSymbols.GetOrAdd(type);
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

            private NodeCache<MethodDesc, GenericLookupResult> _virtualResolveHelpers;

            public GenericLookupResult VirtualMethodAddress(MethodDesc method)
            {
                return _virtualResolveHelpers.GetOrAdd(method);
            }

            private NodeCache<MethodDesc, GenericLookupResult> _methodEntrypoints;

            public GenericLookupResult MethodEntry(MethodDesc method)
            {
                return _methodEntrypoints.GetOrAdd(method);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _objectAllocators;

            public GenericLookupResult ObjectAlloctor(TypeDesc type)
            {
                return _objectAllocators.GetOrAdd(type);
            }

            private NodeCache<TypeDesc, GenericLookupResult> _arrayAllocators;

            public GenericLookupResult ArrayAlloctor(TypeDesc type)
            {
                return _arrayAllocators.GetOrAdd(type);
            }
        }

        public GenericLookupResults GenericLookup = new GenericLookupResults();
    }
}
