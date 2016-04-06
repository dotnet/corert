// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Text;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.TypeParsing;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Runtime.Augments;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.General
{
    //
    // Collect various metadata reading tasks for better chunking...
    //
    internal static class MetadataReaderExtensions
    {
        public static string GetString(this ConstantStringValueHandle handle, MetadataReader reader)
        {
            return reader.GetConstantStringValue(handle).Value;
        }

        // Useful for namespace Name string which can be a null handle.
        public static String GetStringOrNull(this ConstantStringValueHandle handle, MetadataReader reader)
        {
            if (reader.IsNull(handle))
                return null;
            return reader.GetConstantStringValue(handle).Value;
        }

        public static bool StringOrNullEquals(this ConstantStringValueHandle handle, String valueOrNull, MetadataReader reader)
        {
            if (valueOrNull == null)
                return handle.IsNull(reader);
            if (handle.IsNull(reader))
                return false;
            return handle.StringEquals(valueOrNull, reader);
        }

        // Needed for RuntimeMappingTable access
        public static int AsInt(this TypeDefinitionHandle typeDefinitionHandle)
        {
            unsafe
            {
                return *(int*)&typeDefinitionHandle;
            }
        }

        public static TypeDefinitionHandle AsTypeDefinitionHandle(this int i)
        {
            unsafe
            {
                return *(TypeDefinitionHandle*)&i;
            }
        }

        public static int AsInt(this MethodHandle methodHandle)
        {
            unsafe
            {
                return *(int*)&methodHandle;
            }
        }

        public static MethodHandle AsMethodHandle(this int i)
        {
            unsafe
            {
                return *(MethodHandle*)&i;
            }
        }

        public static int AsInt(this FieldHandle fieldHandle)
        {
            unsafe
            {
                return *(int*)&fieldHandle;
            }
        }

        public static FieldHandle AsFieldHandle(this int i)
        {
            unsafe
            {
                return *(FieldHandle*)&i;
            }
        }


        public static bool IsTypeDefRefOrSpecHandle(this Handle handle, MetadataReader reader)
        {
            HandleType handleType = handle.HandleType;
            return handleType == HandleType.TypeDefinition ||
                handleType == HandleType.TypeReference ||
                handleType == HandleType.TypeSpecification;
        }

        public static bool IsNamespaceDefinitionHandle(this Handle handle, MetadataReader reader)
        {
            HandleType handleType = handle.HandleType;
            return handleType == HandleType.NamespaceDefinition;
        }

        public static bool IsNamespaceReferenceHandle(this Handle handle, MetadataReader reader)
        {
            HandleType handleType = handle.HandleType;
            return handleType == HandleType.NamespaceReference;
        }

        // Conversion where a invalid handle type indicates bad metadata rather a mistake by the caller.
        public static ScopeReferenceHandle ToExpectedScopeReferenceHandle(this Handle handle, MetadataReader reader)
        {
            try
            {
                return handle.ToScopeReferenceHandle(reader);
            }
            catch (ArgumentException)
            {
                throw new BadImageFormatException();
            }
        }

        // Conversion where a invalid handle type indicates bad metadata rather a mistake by the caller.
        public static NamespaceReferenceHandle ToExpectedNamespaceReferenceHandle(this Handle handle, MetadataReader reader)
        {
            try
            {
                return handle.ToNamespaceReferenceHandle(reader);
            }
            catch (ArgumentException)
            {
                throw new BadImageFormatException();
            }
        }

        // Conversion where a invalid handle type indicates bad metadata rather a mistake by the caller.
        public static TypeDefinitionHandle ToExpectedTypeDefinitionHandle(this Handle handle, MetadataReader reader)
        {
            try
            {
                return handle.ToTypeDefinitionHandle(reader);
            }
            catch (ArgumentException)
            {
                throw new BadImageFormatException();
            }
        }

        public static MethodSignature ParseMethodSignature(this Handle handle, MetadataReader reader)
        {
            return handle.ToMethodSignatureHandle(reader).GetMethodSignature(reader);
        }

        public static FieldSignature ParseFieldSignature(this Handle handle, MetadataReader reader)
        {
            return handle.ToFieldSignatureHandle(reader).GetFieldSignature(reader);
        }

        public static PropertySignature ParsePropertySignature(this Handle handle, MetadataReader reader)
        {
            return handle.ToPropertySignatureHandle(reader).GetPropertySignature(reader);
        }

        //
        // Used to split methods between DeclaredMethods and DeclaredConstructors.
        //
        public static bool IsConstructor(this MethodHandle methodHandle, MetadataReader reader)
        {
            Method method = methodHandle.GetMethod(reader);
            return IsConstructor(ref method, reader);
        }

        // This is specially designed for a hot path so we make some compromises in the signature:
        //
        //     - "method" is passed by reference even though no side-effects are intended.
        //
        public static bool IsConstructor(ref Method method, MetadataReader reader)
        {
            if ((method.Flags & (MethodAttributes.RTSpecialName | MethodAttributes.SpecialName)) != (MethodAttributes.RTSpecialName | MethodAttributes.SpecialName))
                return false;

            ConstantStringValueHandle nameHandle = method.Name;
            return nameHandle.StringEquals(ConstructorInfo.ConstructorName, reader) || nameHandle.StringEquals(ConstructorInfo.TypeConstructorName, reader);
        }

        private static Exception ParseBoxedEnumConstantValue(this ConstantBoxedEnumValueHandle handle, ReflectionDomain reflectionDomain, MetadataReader reader, out Object value)
        {
            if (!(reflectionDomain is Internal.Reflection.Core.Execution.ExecutionDomain))
                throw new PlatformNotSupportedException(); // Cannot work because boxing enums won't work in non-execution domains.

            ConstantBoxedEnumValue record = handle.GetConstantBoxedEnumValue(reader);

            Exception exception = null;
            Type enumType = reflectionDomain.TryResolve(reader, record.Type, new TypeContext(null, null), ref exception);
            if (enumType == null)
            {
                value = null;
                return exception;
            }

            if (!enumType.GetTypeInfo().IsEnum)
                throw new BadImageFormatException();

            Type underlyingType = Enum.GetUnderlyingType(enumType);

            // Now box the value as the specified enum type.
            unsafe
            {
                switch (record.Value.HandleType)
                {
                    case HandleType.ConstantByteValue:
                        {
                            if (underlyingType != typeof(byte))
                                throw new BadImageFormatException();

                            byte v = record.Value.ToConstantByteValueHandle(reader).GetConstantByteValue(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantSByteValue:
                        {
                            if (underlyingType != typeof(sbyte))
                                throw new BadImageFormatException();

                            sbyte v = record.Value.ToConstantSByteValueHandle(reader).GetConstantSByteValue(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantInt16Value:
                        {
                            if (underlyingType != typeof(short))
                                throw new BadImageFormatException();

                            short v = record.Value.ToConstantInt16ValueHandle(reader).GetConstantInt16Value(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantUInt16Value:
                        {
                            if (underlyingType != typeof(ushort))
                                throw new BadImageFormatException();

                            ushort v = record.Value.ToConstantUInt16ValueHandle(reader).GetConstantUInt16Value(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantInt32Value:
                        {
                            if (underlyingType != typeof(int))
                                throw new BadImageFormatException();

                            int v = record.Value.ToConstantInt32ValueHandle(reader).GetConstantInt32Value(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantUInt32Value:
                        {
                            if (underlyingType != typeof(uint))
                                throw new BadImageFormatException();

                            uint v = record.Value.ToConstantUInt32ValueHandle(reader).GetConstantUInt32Value(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantInt64Value:
                        {
                            if (underlyingType != typeof(long))
                                throw new BadImageFormatException();

                            long v = record.Value.ToConstantInt64ValueHandle(reader).GetConstantInt64Value(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    case HandleType.ConstantUInt64Value:
                        {
                            if (underlyingType != typeof(ulong))
                                throw new BadImageFormatException();

                            ulong v = record.Value.ToConstantUInt64ValueHandle(reader).GetConstantUInt64Value(reader).Value;
                            value = RuntimeAugments.Box(enumType.TypeHandle, (IntPtr)(&v));
                            return null;
                        }
                    default:
                        throw new BadImageFormatException();
                }
            }

        }

        public static Object ParseConstantValue(this Handle handle, ReflectionDomain reflectionDomain, MetadataReader reader)
        {
            Object value;
            Exception exception = handle.TryParseConstantValue(reflectionDomain, reader, out value);
            if (exception != null)
                throw exception;
            return value;
        }

        public static Exception TryParseConstantValue(this Handle handle, ReflectionDomain reflectionDomain, MetadataReader reader, out Object value)
        {
            HandleType handleType = handle.HandleType;
            switch (handleType)
            {
                case HandleType.ConstantBooleanValue:
                    value = handle.ToConstantBooleanValueHandle(reader).GetConstantBooleanValue(reader).Value;
                    return null;
                case HandleType.ConstantStringValue:
                    value = handle.ToConstantStringValueHandle(reader).GetConstantStringValue(reader).Value;
                    return null;
                case HandleType.ConstantCharValue:
                    value = handle.ToConstantCharValueHandle(reader).GetConstantCharValue(reader).Value;
                    return null;
                case HandleType.ConstantByteValue:
                    value = handle.ToConstantByteValueHandle(reader).GetConstantByteValue(reader).Value;
                    return null;
                case HandleType.ConstantSByteValue:
                    value = handle.ToConstantSByteValueHandle(reader).GetConstantSByteValue(reader).Value;
                    return null;
                case HandleType.ConstantInt16Value:
                    value = handle.ToConstantInt16ValueHandle(reader).GetConstantInt16Value(reader).Value;
                    return null;
                case HandleType.ConstantUInt16Value:
                    value = handle.ToConstantUInt16ValueHandle(reader).GetConstantUInt16Value(reader).Value;
                    return null;
                case HandleType.ConstantInt32Value:
                    value = handle.ToConstantInt32ValueHandle(reader).GetConstantInt32Value(reader).Value;
                    return null;
                case HandleType.ConstantUInt32Value:
                    value = handle.ToConstantUInt32ValueHandle(reader).GetConstantUInt32Value(reader).Value;
                    return null;
                case HandleType.ConstantInt64Value:
                    value = handle.ToConstantInt64ValueHandle(reader).GetConstantInt64Value(reader).Value;
                    return null;
                case HandleType.ConstantUInt64Value:
                    value = handle.ToConstantUInt64ValueHandle(reader).GetConstantUInt64Value(reader).Value;
                    return null;
                case HandleType.ConstantSingleValue:
                    value = handle.ToConstantSingleValueHandle(reader).GetConstantSingleValue(reader).Value;
                    return null;
                case HandleType.ConstantDoubleValue:
                    value = handle.ToConstantDoubleValueHandle(reader).GetConstantDoubleValue(reader).Value;
                    return null;
                case HandleType.TypeDefinition:
                case HandleType.TypeReference:
                case HandleType.TypeSpecification:
                    {
                        Exception exception = null;
                        value = reflectionDomain.TryResolve(reader, handle, new TypeContext(null, null), ref exception);
                        return (value == null) ? exception : null;
                    }
                case HandleType.ConstantReferenceValue:
                    value = null;
                    return null;
                case HandleType.ConstantBoxedEnumValue:
                    {
                        return handle.ToConstantBoxedEnumValueHandle(reader).ParseBoxedEnumConstantValue(reflectionDomain, reader, out value);
                    }
                default:
                    {
                        Exception exception;
                        value = handle.TryParseConstantArray(reflectionDomain, reader, out exception);
                        if (value == null)
                            return exception;
                        return null;
                    }
            }
        }

        public static IEnumerable TryParseConstantArray(this Handle handle, ReflectionDomain reflectionDomain, MetadataReader reader, out Exception exception)
        {
            exception = null;

            HandleType handleType = handle.HandleType;
            switch (handleType)
            {
                case HandleType.ConstantBooleanArray:
                    return handle.ToConstantBooleanArrayHandle(reader).GetConstantBooleanArray(reader).Value;

                case HandleType.ConstantStringArray:
                    return handle.ToConstantStringArrayHandle(reader).GetConstantStringArray(reader).Value;

                case HandleType.ConstantCharArray:
                    return handle.ToConstantCharArrayHandle(reader).GetConstantCharArray(reader).Value;

                case HandleType.ConstantByteArray:
                    return handle.ToConstantByteArrayHandle(reader).GetConstantByteArray(reader).Value;

                case HandleType.ConstantSByteArray:
                    return handle.ToConstantSByteArrayHandle(reader).GetConstantSByteArray(reader).Value;

                case HandleType.ConstantInt16Array:
                    return handle.ToConstantInt16ArrayHandle(reader).GetConstantInt16Array(reader).Value;

                case HandleType.ConstantUInt16Array:
                    return handle.ToConstantUInt16ArrayHandle(reader).GetConstantUInt16Array(reader).Value;

                case HandleType.ConstantInt32Array:
                    return handle.ToConstantInt32ArrayHandle(reader).GetConstantInt32Array(reader).Value;

                case HandleType.ConstantUInt32Array:
                    return handle.ToConstantUInt32ArrayHandle(reader).GetConstantUInt32Array(reader).Value;

                case HandleType.ConstantInt64Array:
                    return handle.ToConstantInt64ArrayHandle(reader).GetConstantInt64Array(reader).Value;

                case HandleType.ConstantUInt64Array:
                    return handle.ToConstantUInt64ArrayHandle(reader).GetConstantUInt64Array(reader).Value;

                case HandleType.ConstantSingleArray:
                    return handle.ToConstantSingleArrayHandle(reader).GetConstantSingleArray(reader).Value;

                case HandleType.ConstantDoubleArray:
                    return handle.ToConstantDoubleArrayHandle(reader).GetConstantDoubleArray(reader).Value;

                case HandleType.ConstantHandleArray:
                    {
                        Handle[] constantHandles = handle.ToConstantHandleArrayHandle(reader).GetConstantHandleArray(reader).Value.ToArray();
                        object[] elements = new object[constantHandles.Length];
                        for (int i = 0; i < constantHandles.Length; i++)
                        {
                            exception = constantHandles[i].TryParseConstantValue(reflectionDomain, reader, out elements[i]);
                            if (exception != null)
                                return null;
                        }
                        return elements;
                    }
                default:
                    throw new BadImageFormatException();
            }
        }

        public static Handle GetAttributeTypeHandle(this CustomAttribute customAttribute,
                                                    MetadataReader reader)
        {
            HandleType constructorHandleType = customAttribute.Constructor.HandleType;

            if (constructorHandleType == HandleType.QualifiedMethod)
                return customAttribute.Constructor.ToQualifiedMethodHandle(reader).GetQualifiedMethod(reader).EnclosingType;
            else if (constructorHandleType == HandleType.MemberReference)
                return customAttribute.Constructor.ToMemberReferenceHandle(reader).GetMemberReference(reader).Parent;
            else
                throw new BadImageFormatException();
        }

        //
        // Lightweight check to see if a custom attribute's is of a well-known type.
        //
        // This check performs without instantating the Type object and bloating memory usage. On the flip side,
        // it doesn't check on whether the type is defined in a paricular assembly. The desktop CLR typically doesn't
        // check this either so this is useful from a compat persective as well.
        //
        public static bool IsCustomAttributeOfType(this CustomAttributeHandle customAttributeHandle,
                                                   MetadataReader reader,
                                                   String ns,
                                                   String name)
        {
            String[] namespaceParts = ns.Split('.');
            Handle typeHandle = customAttributeHandle.GetCustomAttribute(reader).GetAttributeTypeHandle(reader);
            HandleType handleType = typeHandle.HandleType;
            if (handleType == HandleType.TypeDefinition)
            {
                TypeDefinition typeDefinition = typeHandle.ToTypeDefinitionHandle(reader).GetTypeDefinition(reader);
                if (!typeDefinition.Name.StringEquals(name, reader))
                    return false;
                NamespaceDefinitionHandle nsHandle = typeDefinition.NamespaceDefinition;
                int idx = namespaceParts.Length;
                while (idx-- != 0)
                {
                    String namespacePart = namespaceParts[idx];
                    NamespaceDefinition namespaceDefinition = nsHandle.GetNamespaceDefinition(reader);
                    if (!namespaceDefinition.Name.StringOrNullEquals(namespacePart, reader))
                        return false;
                    if (!namespaceDefinition.ParentScopeOrNamespace.IsNamespaceDefinitionHandle(reader))
                        return false;
                    nsHandle = namespaceDefinition.ParentScopeOrNamespace.ToNamespaceDefinitionHandle(reader);
                }
                if (!nsHandle.GetNamespaceDefinition(reader).Name.StringOrNullEquals(null, reader))
                    return false;
                return true;
            }
            else if (handleType == HandleType.TypeReference)
            {
                TypeReference typeReference = typeHandle.ToTypeReferenceHandle(reader).GetTypeReference(reader);
                if (!typeReference.TypeName.StringEquals(name, reader))
                    return false;
                if (!typeReference.ParentNamespaceOrType.IsNamespaceReferenceHandle(reader))
                    return false;
                NamespaceReferenceHandle nsHandle = typeReference.ParentNamespaceOrType.ToNamespaceReferenceHandle(reader);
                int idx = namespaceParts.Length;
                while (idx-- != 0)
                {
                    String namespacePart = namespaceParts[idx];
                    NamespaceReference namespaceReference = nsHandle.GetNamespaceReference(reader);
                    if (!namespaceReference.Name.StringOrNullEquals(namespacePart, reader))
                        return false;
                    if (!namespaceReference.ParentScopeOrNamespace.IsNamespaceReferenceHandle(reader))
                        return false;
                    nsHandle = namespaceReference.ParentScopeOrNamespace.ToNamespaceReferenceHandle(reader);
                }
                if (!nsHandle.GetNamespaceReference(reader).Name.StringOrNullEquals(null, reader))
                    return false;
                return true;
            }
            else
                throw new NotSupportedException();
        }


        public static String ToNamespaceName(this NamespaceDefinitionHandle namespaceDefinitionHandle, MetadataReader reader)
        {
            String ns = "";
            for (; ;)
            {
                NamespaceDefinition currentNamespaceDefinition = namespaceDefinitionHandle.GetNamespaceDefinition(reader);
                String name = currentNamespaceDefinition.Name.GetStringOrNull(reader);
                if (name != null)
                {
                    if (ns.Length != 0)
                        ns = "." + ns;
                    ns = name + ns;
                }
                Handle nextHandle = currentNamespaceDefinition.ParentScopeOrNamespace;
                HandleType handleType = nextHandle.HandleType;
                if (handleType == HandleType.ScopeDefinition)
                    break;
                if (handleType == HandleType.NamespaceDefinition)
                {
                    namespaceDefinitionHandle = nextHandle.ToNamespaceDefinitionHandle(reader);
                    continue;
                }

                throw new BadImageFormatException(SR.Bif_InvalidMetadata);
            }
            return ns;
        }

        public static IEnumerable<NamespaceDefinitionHandle> GetTransitiveNamespaces(this MetadataReader reader, IEnumerable<NamespaceDefinitionHandle> namespaceHandles)
        {
            foreach (NamespaceDefinitionHandle namespaceHandle in namespaceHandles)
            {
                yield return namespaceHandle;

                NamespaceDefinition namespaceDefinition = namespaceHandle.GetNamespaceDefinition(reader);
                foreach (NamespaceDefinitionHandle childNamespaceHandle in GetTransitiveNamespaces(reader, namespaceDefinition.NamespaceDefinitions))
                    yield return childNamespaceHandle;
            }
        }

        public static IEnumerable<TypeDefinitionHandle> GetTopLevelTypes(this MetadataReader reader, IEnumerable<NamespaceDefinitionHandle> namespaceHandles)
        {
            foreach (NamespaceDefinitionHandle namespaceHandle in namespaceHandles)
            {
                NamespaceDefinition namespaceDefinition = namespaceHandle.GetNamespaceDefinition(reader);
                foreach (TypeDefinitionHandle typeDefinitionHandle in namespaceDefinition.TypeDefinitions)
                {
                    yield return typeDefinitionHandle;
                }
            }
        }

        public static IEnumerable<TypeDefinitionHandle> GetTransitiveTypes(this MetadataReader reader, IEnumerable<TypeDefinitionHandle> typeDefinitionHandles, bool publicOnly)
        {
            foreach (TypeDefinitionHandle typeDefinitionHandle in typeDefinitionHandles)
            {
                TypeDefinition typeDefinition = typeDefinitionHandle.GetTypeDefinition(reader);

                if (publicOnly)
                {
                    TypeAttributes visibility = typeDefinition.Flags & TypeAttributes.VisibilityMask;
                    if (visibility != TypeAttributes.Public && visibility != TypeAttributes.NestedPublic)
                        continue;
                }

                yield return typeDefinitionHandle;

                foreach (TypeDefinitionHandle nestedTypeDefinitionHandle in GetTransitiveTypes(reader, typeDefinition.NestedTypes, publicOnly))
                    yield return nestedTypeDefinitionHandle;
            }
        }

        public static AssemblyQualifiedTypeName ToAssemblyQualifiedTypeName(this NamespaceReferenceHandle namespaceReferenceHandle, String typeName, MetadataReader reader)
        {
            LowLevelList<String> namespaceParts = new LowLevelList<String>(8);
            NamespaceReference namespaceReference;
            for (; ;)
            {
                namespaceReference = namespaceReferenceHandle.GetNamespaceReference(reader);
                String namespacePart = namespaceReference.Name.GetStringOrNull(reader);
                if (namespacePart == null)
                    break;
                namespaceParts.Add(namespacePart);
                namespaceReferenceHandle = namespaceReference.ParentScopeOrNamespace.ToExpectedNamespaceReferenceHandle(reader);
            }

            ScopeReferenceHandle scopeReferenceHandle = namespaceReference.ParentScopeOrNamespace.ToExpectedScopeReferenceHandle(reader);
            RuntimeAssemblyName assemblyName = scopeReferenceHandle.ToRuntimeAssemblyName(reader);
            return new AssemblyQualifiedTypeName(new NamespaceTypeName(namespaceParts.ToArray(), typeName), assemblyName);
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this ScopeDefinitionHandle scopeDefinitionHandle, MetadataReader reader)
        {
            ScopeDefinition scopeDefinition = scopeDefinitionHandle.GetScopeDefinition(reader);
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                scopeDefinition.Name,
                scopeDefinition.MajorVersion,
                scopeDefinition.MinorVersion,
                scopeDefinition.BuildNumber,
                scopeDefinition.RevisionNumber,
                scopeDefinition.Culture,
                scopeDefinition.PublicKey,
                scopeDefinition.Flags
                );
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this ScopeReferenceHandle scopeReferenceHandle, MetadataReader reader)
        {
            ScopeReference scopeReference = scopeReferenceHandle.GetScopeReference(reader);
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                scopeReference.Name,
                scopeReference.MajorVersion,
                scopeReference.MinorVersion,
                scopeReference.BuildNumber,
                scopeReference.RevisionNumber,
                scopeReference.Culture,
                scopeReference.PublicKeyOrToken,
                scopeReference.Flags
                );
        }

        private static RuntimeAssemblyName CreateRuntimeAssemblyNameFromMetadata(
            MetadataReader reader,
            ConstantStringValueHandle name,
            ushort majorVersion,
            ushort minorVersion,
            ushort buildNumber,
            ushort revisionNumber,
            ConstantStringValueHandle culture,
            IEnumerable<byte> publicKeyOrToken,
            AssemblyFlags assemblyFlags)
        {
            AssemblyNameFlags assemblyNameFlags = AssemblyNameFlags.None;
            if (0 != (assemblyFlags & AssemblyFlags.PublicKey))
                assemblyNameFlags |= AssemblyNameFlags.PublicKey;
            if (0 != (assemblyFlags & AssemblyFlags.Retargetable))
                assemblyNameFlags |= AssemblyNameFlags.Retargetable;
            int contentType = ((int)assemblyFlags) & 0x00000E00;
            assemblyNameFlags |= (AssemblyNameFlags)contentType;

            return new RuntimeAssemblyName(
                name.GetString(reader),
                new Version(majorVersion, minorVersion, buildNumber, revisionNumber),
                culture.GetStringOrNull(reader),
                assemblyNameFlags,
                publicKeyOrToken.ToArray()
                );
        }
    }
}

