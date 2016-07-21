// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Diagnostics;
using global::System.Collections;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.Assemblies;

using global::Internal.Metadata.NativeFormat;

using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.TypeParsing
{
    //
    // The TypeName class is the base class for a family of types that represent the nodes in a parse tree for 
    // assembly-qualified type names.
    //
    internal abstract class TypeName
    {
        public abstract Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result);
        public abstract override String ToString();
    }


    //
    // Represents a parse of a type name OPTIONALLY qualified by an assembly name. If present, the assembly name follows
    // a comma following the type name.
    //
    // Note that unlike the reflection model, the assembly qualification is a property of a typename string as a whole
    // rather than the property of the single namespace type that "represents" the type. This model is simply a better match to
    // how type names passed to GetType() are constructed and parsed.
    //
    internal sealed class AssemblyQualifiedTypeName : TypeName
    {
        public AssemblyQualifiedTypeName(NonQualifiedTypeName typeName, RuntimeAssemblyName assemblyName)
        {
            Debug.Assert(typeName != null);
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        public NonQualifiedTypeName TypeName { get; private set; }
        public RuntimeAssemblyName AssemblyName { get; private set; }  // This can return null if the type name was not actually qualified.

        public sealed override String ToString()
        {
            return TypeName.ToString() + ((AssemblyName == null) ? "" : ", " + AssemblyName.FullName);
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            if (AssemblyName == null)
            {
                return TypeName.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out result);
            }
            else
            {
                RuntimeAssembly newAssembly;
                Exception assemblyLoadException = RuntimeAssembly.TryGetRuntimeAssembly(reflectionDomain, AssemblyName, out newAssembly);
                if (assemblyLoadException != null)
                    return assemblyLoadException;
                return TypeName.TryResolve(reflectionDomain, newAssembly, ignoreCase, out result);
            }
        }
    }

    //
    // Base class for all non-assembly-qualified type names.
    //
    internal abstract class NonQualifiedTypeName : TypeName
    {
    }

    //
    // Base class for namespace or nested type.
    //
    internal abstract class NamedTypeName : NonQualifiedTypeName
    {
    }

    //
    // Non-nested named type. The full name is the namespace-qualified name. For example, the FullName for
    // System.Collections.Generic.IList<> is "System.Collections.Generic.IList`1".
    //
    internal sealed partial class NamespaceTypeName : NamedTypeName
    {
        public NamespaceTypeName(String[] namespaceParts, String name)
        {
            Debug.Assert(namespaceParts != null);
            Debug.Assert(name != null);

            _name = name;
            _namespaceParts = namespaceParts;
        }

        public sealed override String ToString()
        {
            String fullName = "";
            for (int i = 0; i < _namespaceParts.Length; i++)
            {
                fullName += _namespaceParts[_namespaceParts.Length - i - 1];
                fullName += ".";
            }
            fullName += _name;
            return fullName;
        }

        private bool TryResolveNamespaceDefinitionCaseSensitive(MetadataReader reader, ScopeDefinitionHandle scopeDefinitionHandle, out NamespaceDefinition namespaceDefinition)
        {
            namespaceDefinition = scopeDefinitionHandle.GetScopeDefinition(reader).RootNamespaceDefinition.GetNamespaceDefinition(reader);
            IEnumerable<NamespaceDefinitionHandle> candidates = namespaceDefinition.NamespaceDefinitions;
            int idx = _namespaceParts.Length;
            while (idx-- != 0)
            {
                // Each iteration finds a match for one segment of the namespace chain.
                String expected = _namespaceParts[idx];
                bool foundMatch = false;
                foreach (NamespaceDefinitionHandle candidate in candidates)
                {
                    namespaceDefinition = candidate.GetNamespaceDefinition(reader);
                    if (namespaceDefinition.Name.StringOrNullEquals(expected, reader))
                    {
                        // Found a match for this segment of the namespace chain. Move on to the next level.
                        foundMatch = true;
                        candidates = namespaceDefinition.NamespaceDefinitions;
                        break;
                    }
                }

                if (!foundMatch)
                {
                    return false;
                }
            }

            return true;
        }

        private Exception UncachedTryResolveCaseSensitive(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, out RuntimeTypeInfo result)
        {
            result = null;

            foreach (QScopeDefinition scopeDefinition in currentAssembly.AllScopes)
            {
                MetadataReader reader = scopeDefinition.Reader;
                ScopeDefinitionHandle scopeDefinitionHandle = scopeDefinition.Handle;

                NamespaceDefinition namespaceDefinition;
                if (!TryResolveNamespaceDefinitionCaseSensitive(reader, scopeDefinitionHandle, out namespaceDefinition))
                {
                    continue;
                }

                // We've successfully drilled down the namespace chain. Now look for a top-level type matching the type name.
                IEnumerable<TypeDefinitionHandle> candidateTypes = namespaceDefinition.TypeDefinitions;
                foreach (TypeDefinitionHandle candidateType in candidateTypes)
                {
                    TypeDefinition typeDefinition = candidateType.GetTypeDefinition(reader);
                    if (typeDefinition.Name.StringEquals(_name, reader))
                    {
                        result = reflectionDomain.ResolveTypeDefinition(reader, candidateType);
                        return null;
                    }
                }

                // No match found in this assembly - see if there's a matching type forwarder.
                IEnumerable<TypeForwarderHandle> candidateTypeForwarders = namespaceDefinition.TypeForwarders;
                foreach (TypeForwarderHandle typeForwarderHandle in candidateTypeForwarders)
                {
                    TypeForwarder typeForwarder = typeForwarderHandle.GetTypeForwarder(reader);
                    if (typeForwarder.Name.StringEquals(_name, reader))
                    {
                        RuntimeAssemblyName redirectedAssemblyName = typeForwarder.Scope.ToRuntimeAssemblyName(reader);
                        AssemblyQualifiedTypeName redirectedTypeName = new AssemblyQualifiedTypeName(this, redirectedAssemblyName);
                        return redirectedTypeName.TryResolve(reflectionDomain, null, /*ignoreCase: */false, out result);
                    }
                }
            }

            {
                String typeName = this.ToString();
                String message = SR.Format(SR.TypeLoad_TypeNotFound, typeName, currentAssembly.FullName);
                return ReflectionCoreNonPortable.CreateTypeLoadException(message, typeName);
            }
        }

        private Exception TryResolveCaseInsensitive(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, out RuntimeTypeInfo result)
        {
            String fullName = this.ToString().ToLower();

            LowLevelDictionary<String, QHandle> dict = GetCaseInsensitiveTypeDictionary(currentAssembly);
            QHandle qualifiedHandle;
            if (!dict.TryGetValue(fullName, out qualifiedHandle))
            {
                result = null;
                return new TypeLoadException(SR.Format(SR.TypeLoad_TypeNotFound, this.ToString(), currentAssembly.FullName));
            }

            MetadataReader reader = qualifiedHandle.Reader;
            Handle typeDefOrForwarderHandle = qualifiedHandle.Handle;

            HandleType handleType = typeDefOrForwarderHandle.HandleType;
            switch (handleType)
            {
                case HandleType.TypeDefinition:
                    {
                        TypeDefinitionHandle typeDefinitionHandle = typeDefOrForwarderHandle.ToTypeDefinitionHandle(reader);
                        result = reflectionDomain.ResolveTypeDefinition(reader, typeDefinitionHandle);
                        return null;
                    }
                case HandleType.TypeForwarder:
                    {
                        TypeForwarder typeForwarder = typeDefOrForwarderHandle.ToTypeForwarderHandle(reader).GetTypeForwarder(reader);
                        ScopeReferenceHandle destinationScope = typeForwarder.Scope;
                        RuntimeAssemblyName destinationAssemblyName = destinationScope.ToRuntimeAssemblyName(reader);
                        RuntimeAssembly destinationAssembly;
                        Exception exception = RuntimeAssembly.TryGetRuntimeAssembly(reflectionDomain, destinationAssemblyName, out destinationAssembly);
                        if (exception != null)
                        {
                            result = null;
                            return exception;
                        }
                        return TryResolveCaseInsensitive(reflectionDomain, destinationAssembly, out result);
                    }
                default:
                    throw new InvalidOperationException();
            }
        }

        private static LowLevelDictionary<String, QHandle> CreateCaseInsensitiveTypeDictionary(RuntimeAssembly assembly)
        {
            //
            // Collect all of the *non-nested* types and type-forwards. 
            //
            //   The keys are full typenames in lower-cased form.
            //   The value is a tuple containing either a TypeDefinitionHandle or TypeForwarderHandle and the associated Reader
            //      for that handle.
            //
            // We do not store nested types here. The container type is resolved and chosen first, then the nested type chosen from 
            // that. If we chose the wrong container type and fail the match as a result, that's too bad. (The desktop CLR has the
            // same issue.)
            //
            ReflectionDomain reflectionDomain = assembly.ReflectionDomain;
            LowLevelDictionary<String, QHandle> dict = new LowLevelDictionary<string, QHandle>();

            foreach (QScopeDefinition scope in assembly.AllScopes)
            {
                MetadataReader reader = scope.Reader;
                ScopeDefinition scopeDefinition = scope.ScopeDefinition;
                IEnumerable<NamespaceDefinitionHandle> topLevelNamespaceHandles = new NamespaceDefinitionHandle[] { scopeDefinition.RootNamespaceDefinition };
                IEnumerable<NamespaceDefinitionHandle> allNamespaceHandles = reader.GetTransitiveNamespaces(topLevelNamespaceHandles);
                foreach (NamespaceDefinitionHandle namespaceHandle in allNamespaceHandles)
                {
                    String ns = namespaceHandle.ToNamespaceName(reader);
                    if (ns.Length != 0)
                        ns = ns + ".";
                    ns = ns.ToLower();

                    NamespaceDefinition namespaceDefinition = namespaceHandle.GetNamespaceDefinition(reader);
                    foreach (TypeDefinitionHandle typeDefinitionHandle in namespaceDefinition.TypeDefinitions)
                    {
                        String fullName = ns + typeDefinitionHandle.GetTypeDefinition(reader).Name.GetString(reader).ToLower();
                        QHandle existingValue;
                        if (!dict.TryGetValue(fullName, out existingValue))
                        {
                            dict.Add(fullName, new QHandle(reader, typeDefinitionHandle));
                        }
                    }

                    foreach (TypeForwarderHandle typeForwarderHandle in namespaceDefinition.TypeForwarders)
                    {
                        String fullName = ns + typeForwarderHandle.GetTypeForwarder(reader).Name.GetString(reader).ToLower();
                        QHandle existingValue;
                        if (!dict.TryGetValue(fullName, out existingValue))
                        {
                            dict.Add(fullName, new QHandle(reader, typeForwarderHandle));
                        }
                    }
                }
            }

            return dict;
        }

        private String _name;
        private String[] _namespaceParts;
    }

    //
    // A nested type. The Name is the simple name of the type (not including any portion of its declaring type name.
    //
    internal sealed class NestedTypeName : NamedTypeName
    {
        public NestedTypeName(String name, NamedTypeName declaringType)
        {
            Name = name;
            DeclaringType = declaringType;
        }

        public String Name { get; private set; }
        public NamedTypeName DeclaringType { get; private set; }

        public sealed override String ToString()
        {
            return DeclaringType + "+" + Name;
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            RuntimeTypeInfo declaringType;
            Exception typeLoadException = DeclaringType.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out declaringType);
            if (typeLoadException != null)
                return typeLoadException;
            TypeInfo nestedTypeInfo = FindDeclaredNestedType(declaringType, Name, ignoreCase);
            if (nestedTypeInfo == null)
                return new TypeLoadException(SR.Format(SR.TypeLoad_TypeNotFound, declaringType.FullName + "+" + Name, currentAssembly.FullName));
            result = nestedTypeInfo.CastToRuntimeTypeInfo();
            return null;
        }

        private TypeInfo FindDeclaredNestedType(TypeInfo declaringTypeInfo, String name, bool ignoreCase)
        {
            TypeInfo nestedType = declaringTypeInfo.GetDeclaredNestedType(name);
            if (nestedType != null)
                return nestedType;
            if (!ignoreCase)
                return null;

            //
            // Desktop compat note: If there is more than one nested type that matches the name in a case-blind match,
            // we might not return the same one that the desktop returns. The actual selection method is influenced both by the type's
            // placement in the IL and the implementation details of the CLR's internal hashtables so it would be very
            // hard to replicate here.
            //
            // Desktop compat note #2: Case-insensitive lookups: If we don't find a match, we do *not* go back and search
            // other declaring types that might match the case-insensitive search and contain the nested type being sought.
            // Though this is somewhat unsatisfactory, the desktop CLR has the same limitation.
            //
            foreach (TypeInfo candidate in declaringTypeInfo.DeclaredNestedTypes)
            {
                String candidateName = candidate.Name;
                if (name.Equals(candidateName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }
    }

    //
    // Abstract base for array, byref and pointer type names.
    //
    internal abstract class HasElementTypeName : NonQualifiedTypeName
    {
        public HasElementTypeName(TypeName elementTypeName)
        {
            ElementTypeName = elementTypeName;
        }

        public TypeName ElementTypeName { get; private set; }
    }

    //
    // A single-dimensional zero-lower-bound array type name.
    //
    internal sealed class ArrayTypeName : HasElementTypeName
    {
        public ArrayTypeName(TypeName elementTypeName)
            : base(elementTypeName)
        {
        }

        public sealed override String ToString()
        {
            return ElementTypeName + "[]";
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            RuntimeTypeInfo elementType;
            Exception typeLoadException = ElementTypeName.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out elementType);
            if (typeLoadException != null)
                return typeLoadException;
            result = elementType.GetArrayType();
            return null;
        }
    }

    //
    // A multidim array type name.
    //
    internal sealed class MultiDimArrayTypeName : HasElementTypeName
    {
        public MultiDimArrayTypeName(TypeName elementTypeName, int rank)
            : base(elementTypeName)
        {
            _rank = rank;
        }

        public sealed override String ToString()
        {
            return ElementTypeName + "[" + (_rank == 1 ? "*" : new String(',', _rank - 1)) + "]";
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            RuntimeTypeInfo elementType;
            Exception typeLoadException = ElementTypeName.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out elementType);
            if (typeLoadException != null)
                return typeLoadException;
            result = elementType.GetMultiDimArrayType(_rank);
            return null;
        }

        private int _rank;
    }

    //
    // A byref type.
    //
    internal sealed class ByRefTypeName : HasElementTypeName
    {
        public ByRefTypeName(TypeName elementTypeName)
            : base(elementTypeName)
        {
        }

        public sealed override String ToString()
        {
            return ElementTypeName + "&";
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            RuntimeTypeInfo elementType;
            Exception typeLoadException = ElementTypeName.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out elementType);
            if (typeLoadException != null)
                return typeLoadException;
            result = elementType.GetByRefType();
            return null;
        }
    }

    //
    // A pointer type.
    //
    internal sealed class PointerTypeName : HasElementTypeName
    {
        public PointerTypeName(TypeName elementTypeName)
            : base(elementTypeName)
        {
        }

        public sealed override String ToString()
        {
            return ElementTypeName + "*";
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            RuntimeTypeInfo elementType;
            Exception typeLoadException = ElementTypeName.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out elementType);
            if (typeLoadException != null)
                return typeLoadException;
            result = elementType.GetPointerType();
            return null;
        }
    }

    //
    // A constructed generic type.
    //
    internal sealed class ConstructedGenericTypeName : NonQualifiedTypeName
    {
        public ConstructedGenericTypeName(NamedTypeName genericType, IEnumerable<TypeName> genericArguments)
        {
            GenericType = genericType;
            GenericArguments = genericArguments;
        }

        public NamedTypeName GenericType { get; private set; }
        public IEnumerable<TypeName> GenericArguments { get; private set; }

        public sealed override String ToString()
        {
            String s = GenericType.ToString();
            s += "[";
            String sep = "";
            foreach (TypeName genericTypeArgument in GenericArguments)
            {
                s += sep;
                sep = ",";
                AssemblyQualifiedTypeName assemblyQualifiedTypeArgument = genericTypeArgument as AssemblyQualifiedTypeName;
                if (assemblyQualifiedTypeArgument == null || assemblyQualifiedTypeArgument.AssemblyName == null)
                    s += genericTypeArgument.ToString();
                else
                    s += "[" + genericTypeArgument.ToString() + "]";
            }
            s += "]";
            return s;
        }

        public sealed override Exception TryResolve(ReflectionDomain reflectionDomain, RuntimeAssembly currentAssembly, bool ignoreCase, out RuntimeTypeInfo result)
        {
            result = null;
            RuntimeTypeInfo genericType;
            Exception typeLoadException = GenericType.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out genericType);
            if (typeLoadException != null)
                return typeLoadException;
            LowLevelList<RuntimeTypeInfo> genericTypeArguments = new LowLevelList<RuntimeTypeInfo>();
            foreach (TypeName genericTypeArgumentName in GenericArguments)
            {
                RuntimeTypeInfo genericTypeArgument;
                typeLoadException = genericTypeArgumentName.TryResolve(reflectionDomain, currentAssembly, ignoreCase, out genericTypeArgument);
                if (typeLoadException != null)
                    return typeLoadException;
                genericTypeArguments.Add(genericTypeArgument);
            }
            result = genericType.GetConstructedGenericType(genericTypeArguments.ToArray());
            return null;
        }
    }
}
