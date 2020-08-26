// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

using Internal.Metadata.NativeFormat;

using Debug = System.Diagnostics.Debug;

namespace System.Private.Reflection.Metadata.Tests
{
    static class CollectionExtensions
    {
        public static ScopeDefinitionHandle Single(this ScopeDefinitionHandleCollection collection)
        {
            Debug.Assert(collection.Count == 1);
            var enumerator = collection.GetEnumerator();
            bool hasNext = enumerator.MoveNext();
            Debug.Assert(hasNext);
            var result = enumerator.Current;
            Debug.Assert(!enumerator.MoveNext());
            return result;
        }

        public static TypeDefinitionHandle Single(this TypeDefinitionHandleCollection collection)
        {
            Debug.Assert(collection.Count == 1);
            var enumerator = collection.GetEnumerator();
            bool hasNext = enumerator.MoveNext();
            Debug.Assert(hasNext);
            var result = enumerator.Current;
            Debug.Assert(!enumerator.MoveNext());
            return result;
        }

        public static NamespaceDefinitionHandle Single(this NamespaceDefinitionHandleCollection collection)
        {
            Debug.Assert(collection.Count == 1);
            var enumerator = collection.GetEnumerator();
            bool hasNext = enumerator.MoveNext();
            Debug.Assert(hasNext);
            var result = enumerator.Current;
            Debug.Assert(!enumerator.MoveNext());
            return result;
        }

        public static MethodHandle Single(this MethodHandleCollection collection)
        {
            Debug.Assert(collection.Count == 1);
            var enumerator = collection.GetEnumerator();
            bool hasNext = enumerator.MoveNext();
            Debug.Assert(hasNext);
            var result = enumerator.Current;
            Debug.Assert(!enumerator.MoveNext());
            return result;
        }

        public static IEnumerable<NamespaceDefinitionHandle> AsEnumerable(this NamespaceDefinitionHandleCollection collection)
        {
            foreach (var element in collection)
                yield return element;
        }

        public static IEnumerable<TypeDefinitionHandle> AsEnumerable(this TypeDefinitionHandleCollection collection)
        {
            foreach (var element in collection)
                yield return element;
        }
    }
}
