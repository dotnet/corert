// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;
using System.Linq;

namespace MetadataTransformTests
{
    static class NativeFormatExtensions
    {
        public static IEnumerable<TypeDefinition> GetAllTypes(this ScopeDefinition scope)
        {
            return scope.RootNamespaceDefinition.GetAllTypes();
        }

        private static IEnumerable<TypeDefinition> GetAllTypes(this NamespaceDefinition ns)
        {
            return ns.TypeDefinitions.SelectMany(t => t.GetAllTypes()).Concat(ns.NamespaceDefinitions.SelectMany(n => n.GetAllTypes()));
        }

        private static IEnumerable<TypeDefinition> GetAllTypes(this TypeDefinition type)
        {
            return Enumerable.Repeat(type, 1).Concat(type.NestedTypes.SelectMany(n => n.GetAllTypes()));
        }
    }
}
