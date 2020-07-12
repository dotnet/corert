// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Reflection.Runtime.General;

namespace Internal.TypeSystem.NativeFormat
{
    public static class MetadataExtensions
    {
        public static bool HasCustomAttribute(this MetadataReader metadataReader, CustomAttributeHandleCollection customAttributes,
            string attributeNamespace, string attributeName)
        {
            foreach (var attributeHandle in customAttributes)
            {
                ConstantStringValueHandle nameHandle;
                string namespaceName;
                if (!metadataReader.GetAttributeNamespaceAndName(attributeHandle, out namespaceName, out nameHandle))
                    continue;

                if (namespaceName.Equals(attributeNamespace)
                    && nameHandle.StringEquals(attributeName, metadataReader))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool GetAttributeNamespaceAndName(this MetadataReader metadataReader, CustomAttributeHandle attributeHandle,
            out string namespaceString, out ConstantStringValueHandle nameHandle)
        {
            Handle attributeType, attributeCtor;
            if (!GetAttributeTypeAndConstructor(metadataReader, attributeHandle, out attributeType, out attributeCtor))
            {
                namespaceString = null;
                nameHandle = default(ConstantStringValueHandle);
                return false;
            }

            return GetAttributeTypeNamespaceAndName(metadataReader, attributeType, out namespaceString, out nameHandle);
        }

        private static Handle GetAttributeTypeHandle(this CustomAttribute customAttribute, MetadataReader reader)
        {
            HandleType constructorHandleType = customAttribute.Constructor.HandleType;

            if (constructorHandleType == HandleType.QualifiedMethod)
                return customAttribute.Constructor.ToQualifiedMethodHandle(reader).GetQualifiedMethod(reader).EnclosingType;
            else if (constructorHandleType == HandleType.MemberReference)
                return customAttribute.Constructor.ToMemberReferenceHandle(reader).GetMemberReference(reader).Parent;
            else
                throw new BadImageFormatException();
        }

        public static bool GetAttributeTypeAndConstructor(this MetadataReader metadataReader, CustomAttributeHandle attributeHandle,
            out Handle attributeType, out Handle attributeCtor)
        {
            CustomAttribute attribute = metadataReader.GetCustomAttribute(attributeHandle);
            attributeCtor = attribute.Constructor;
            attributeType = attribute.GetAttributeTypeHandle(metadataReader);
            return true;
        }

        public static bool GetAttributeTypeNamespaceAndName(this MetadataReader metadataReader, Handle attributeType,
            out string namespaceString, out ConstantStringValueHandle nameHandle)
        {
            namespaceString = null;
            nameHandle = default(ConstantStringValueHandle);

            if (attributeType.HandleType == HandleType.TypeReference)
            {
                TypeReference typeRefRow = metadataReader.GetTypeReference(attributeType.ToTypeReferenceHandle(metadataReader));
                HandleType handleType = typeRefRow.ParentNamespaceOrType.HandleType;

                // Nested type?
                if (handleType == HandleType.TypeReference || handleType == HandleType.TypeDefinition)
                    return false;

                nameHandle = typeRefRow.TypeName;
                namespaceString = metadataReader.GetNamespaceName(typeRefRow.ParentNamespaceOrType.ToNamespaceReferenceHandle(metadataReader));
                return true;
            }
            else if (attributeType.HandleType == HandleType.TypeDefinition)
            {
                var def = metadataReader.GetTypeDefinition(attributeType.ToTypeDefinitionHandle(metadataReader));

                // Nested type?
                if (IsNested(def.Flags))
                    return false;

                nameHandle = def.Name;
                namespaceString = metadataReader.GetNamespaceName(def.NamespaceDefinition);
                return true;
            }
            else
            {
                // unsupported metadata
                return false;
            }
        }

        internal static string GetNamespaceName(this MetadataReader metadataReader, NamespaceDefinitionHandle namespaceDefinitionHandle)
        {
            if (namespaceDefinitionHandle.IsNull(metadataReader))
            {
                return null;
            }
            else
            {
                // TODO! Cache this result, or do something to make it more efficient.
                NamespaceDefinition namespaceDefinition = namespaceDefinitionHandle.GetNamespaceDefinition(metadataReader);
                string name = metadataReader.GetString(namespaceDefinition.Name) ?? "";
                if (namespaceDefinition.ParentScopeOrNamespace.HandleType == HandleType.NamespaceDefinition)
                {
                    string parentName = metadataReader.GetNamespaceName(namespaceDefinition.ParentScopeOrNamespace.ToNamespaceDefinitionHandle(metadataReader));
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        name = parentName + "." + name;
                    }
                }
                return name;
            }
        }

        internal static string GetNamespaceName(this MetadataReader metadataReader, NamespaceReferenceHandle namespaceReferenceHandle)
        {
            if (namespaceReferenceHandle.IsNull(metadataReader))
            {
                return null;
            }
            else
            {
                // TODO! Cache this result, or do something to make it more efficient.
                NamespaceReference namespaceReference = namespaceReferenceHandle.GetNamespaceReference(metadataReader);
                string name = metadataReader.GetString(namespaceReference.Name) ?? "";
                if (namespaceReference.ParentScopeOrNamespace.HandleType == HandleType.NamespaceReference)
                {
                    string parentName = metadataReader.GetNamespaceName(namespaceReference.ParentScopeOrNamespace.ToNamespaceReferenceHandle(metadataReader));
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        name = parentName + "." + name;
                    }
                }
                return name;
            }
        }

        // This mask is the fastest way to check if a type is nested from its flags,
        // but it should not be added to the BCL enum as its semantics can be misleading.
        // Consider, for example, that (NestedFamANDAssem & NestedMask) == NestedFamORAssem.
        // Only comparison of the masked value to 0 is meaningful, which is different from
        // the other masks in the enum.
        private const TypeAttributes NestedMask = (TypeAttributes)0x00000006;

        private static bool IsNested(TypeAttributes flags)
        {
            return (flags & NestedMask) != 0;
        }


        public static bool IsRuntimeSpecialName(this MethodAttributes flags)
        {
            return (flags & (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName))
                == (MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
        }

        public static bool IsPublic(this MethodAttributes flags)
        {
            return (flags & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
        }

        /// <summary>
        /// Convert a metadata Handle to a integer (that can be round-tripped back into a handle)
        /// </summary>
        public static int ToInt(this Handle handle)
        {
            // This is gross, but its the only api I can find that directly returns the handle into a token
            // The assert is used to verify this round-trips properly
            int handleAsToken = handle.GetHashCode();
            Debug.Assert(handleAsToken.AsHandle().Equals(handle));

            return handleAsToken;
        }

        /// <summary>
        /// Convert a metadata MethodHandle to a integer (that can be round-tripped back into a handle)
        /// This differs from the above function only be parameter type.
        /// </summary>
        public static int ToInt(this MethodHandle handle)
        {
            // This is gross, but its the only api I can find that directly returns the handle into a token
            // The assert is used to verify this round-trips properly
            int handleAsToken = handle.GetHashCode();
            Debug.Assert(handleAsToken.AsHandle().Equals(handle));

            return handleAsToken;
        }

        public static byte[] ConvertByteCollectionToArray(this ByteCollection collection)
        {
            byte[] array = new byte[collection.Count];
            int i = 0;
            foreach (byte b in collection)
                array[i++] = b;

            return array;
        }
    }
}
