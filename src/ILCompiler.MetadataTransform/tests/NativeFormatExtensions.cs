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
            yield return type;

            foreach (var nestedType in type.NestedTypes)
                yield return nestedType;
        }
    }
}
