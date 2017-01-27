// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Runtime.Assemblies;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;

using Internal.Runtime.Augments;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.General
{
    //
    // Collect various metadata reading tasks for better chunking...
    //
    internal static class EcmaMetadataReaderExtensions
    {
        public static string GetString(this StringHandle handle, MetadataReader reader)
        {
            return reader.GetString(handle);
        }

        public static string GetStringOrNull(this StringHandle handle, MetadataReader reader)
        {
            if (handle.IsNil)
                return null;

            return reader.GetString(handle);
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyDefinition assemblyDefinition, MetadataReader reader)
        {
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                assemblyDefinition.Name,
                assemblyDefinition.Version,
                assemblyDefinition.Culture,
                assemblyDefinition.PublicKey,
                assemblyDefinition.Flags
                );
        }

        public static RuntimeAssemblyName ToRuntimeAssemblyName(this AssemblyReferenceHandle assemblyReferenceHandle, MetadataReader reader)
        {
            AssemblyReference assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
            return CreateRuntimeAssemblyNameFromMetadata(
                reader,
                assemblyReference.Name,
                assemblyReference.Version,
                assemblyReference.Culture,
                assemblyReference.PublicKeyOrToken,
                assemblyReference.Flags
                );
        }

        private static RuntimeAssemblyName CreateRuntimeAssemblyNameFromMetadata(
            MetadataReader reader,
            StringHandle name,
            Version version,
            StringHandle culture,
            BlobHandle publicKeyOrToken,
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
                version,
                culture.GetString(reader),
                assemblyNameFlags,
                reader.GetBlobContent(publicKeyOrToken).ToArray()
                );
        }

        //
        // Used to split methods between DeclaredMethods and DeclaredConstructors.
        //
        public static bool IsConstructor(this MethodDefinitionHandle methodHandle, MetadataReader reader)
        {
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            return IsConstructor(ref method, reader);
        }

        // This is specially designed for a hot path so we make some compromises in the signature:
        //
        //     - "method" is passed by reference even though no side-effects are intended.
        //
        public static bool IsConstructor(ref MethodDefinition method, MetadataReader reader)
        {
            if ((method.Attributes & (MethodAttributes.RTSpecialName | MethodAttributes.SpecialName)) != (MethodAttributes.RTSpecialName | MethodAttributes.SpecialName))
                return false;

            MetadataStringComparer stringComparer = reader.StringComparer;

            StringHandle nameHandle = method.Name;
            return stringComparer.Equals(nameHandle, ConstructorInfo.ConstructorName) || stringComparer.Equals(nameHandle, ConstructorInfo.TypeConstructorName);
        }


        /// <summary>
        /// Given a token for a constructor, return the token for the constructor's type and the blob containing the
        /// constructor's signature.
        /// </summary>
        public static void GetAttributeTypeDefRefOrSpecHandle(
            MetadataReader metadataReader,
            EntityHandle attributeCtor,
            out EntityHandle ctorType)
        {
            ctorType = default(EntityHandle);

            if (attributeCtor.Kind == HandleKind.MemberReference)
            {
                MemberReference memberRef = metadataReader.GetMemberReference((MemberReferenceHandle)attributeCtor);

                StringHandle ctorName = memberRef.Name;

                if (!metadataReader.StringComparer.Equals(ctorName, ConstructorInfo.ConstructorName))
                {
                    // Not a constructor.
                    throw new BadImageFormatException();
                }

                ctorType = memberRef.Parent;
            }
            else if (attributeCtor.Kind == HandleKind.MethodDefinition)
            {
                var methodDef = metadataReader.GetMethodDefinition((MethodDefinitionHandle)attributeCtor);

                if (!metadataReader.StringComparer.Equals(methodDef.Name, ConstructorInfo.ConstructorName))
                {
                    // Not a constructor.
                    throw new BadImageFormatException();
                }

                ctorType = methodDef.GetDeclaringType();
                Debug.Assert(!ctorType.IsNil);
            }
            else
            {
                // invalid metadata
                throw new BadImageFormatException();
            }
        }


        /// <summary>
        /// Given a token for a type, return the type's name and namespace.  Only works for top level types. 
        /// namespaceHandle will be NamespaceDefinitionHandle for defs and StringHandle for refs. 
        /// </summary>
        /// <returns>True if the function successfully returns the name and namespace.</returns>
        public static bool GetAttributeNamespaceAndName(MetadataReader metadataReader, EntityHandle typeDefOrRef, out StringHandle namespaceHandle, out StringHandle nameHandle)
        {
            nameHandle = default(StringHandle);
            namespaceHandle = default(StringHandle);

            if (typeDefOrRef.Kind == HandleKind.TypeReference)
            {
                TypeReference typeRefRow = metadataReader.GetTypeReference((TypeReferenceHandle)typeDefOrRef);
                HandleKind handleType = typeRefRow.ResolutionScope.Kind;

                if (handleType == HandleKind.TypeReference || handleType == HandleKind.TypeDefinition)
                {
                    // TODO - Support nested types.  
                    return false;
                }

                nameHandle = typeRefRow.Name;
                namespaceHandle = typeRefRow.Namespace;
            }
            else if (typeDefOrRef.Kind == HandleKind.TypeDefinition)
            {
                var def = metadataReader.GetTypeDefinition((TypeDefinitionHandle)typeDefOrRef);

                if (IsNested(def.Attributes))
                {
                    // TODO - Support nested types. 
                    return false;
                }

                nameHandle = def.Name;
                namespaceHandle = def.Namespace;
            }
            else
            {
                // unsupported metadata
                throw new BadImageFormatException();
            }

            return true;
        }


        public static bool IsNested(TypeAttributes flags)
        {
            return (flags & ((TypeAttributes)0x00000006)) != 0;
        }

        internal enum CorElementType : byte
        {
            Invalid,
            ELEMENT_TYPE_VOID,
            ELEMENT_TYPE_BOOLEAN,
            ELEMENT_TYPE_CHAR,
            ELEMENT_TYPE_I1,
            ELEMENT_TYPE_U1,
            ELEMENT_TYPE_I2,
            ELEMENT_TYPE_U2,
            ELEMENT_TYPE_I4,
            ELEMENT_TYPE_U4,
            ELEMENT_TYPE_I8,
            ELEMENT_TYPE_U8,
            ELEMENT_TYPE_R4,
            ELEMENT_TYPE_R8,
            ELEMENT_TYPE_STRING,
            ELEMENT_TYPE_PTR,
            ELEMENT_TYPE_BYREF,
            ELEMENT_TYPE_VALUETYPE,
            ELEMENT_TYPE_CLASS,
            ELEMENT_TYPE_VAR,
            ELEMENT_TYPE_ARRAY,
            ELEMENT_TYPE_GENERICINST,
            ELEMENT_TYPE_TYPEDBYREF,
            ELEMENT_TYPE_I = 24,
            ELEMENT_TYPE_U,
            ELEMENT_TYPE_FNPTR = 27,
            ELEMENT_TYPE_OBJECT,
            ELEMENT_TYPE_SZARRAY,
            ELEMENT_TYPE_MVAR,
            ELEMENT_TYPE_CMOD_REQD,
            ELEMENT_TYPE_CMOD_OPT,
            ELEMENT_TYPE_HANDLE = 64,
            ELEMENT_TYPE_SENTINEL,
            ELEMENT_TYPE_PINNED = 69
        }

        public static void SkipType(ref BlobReader reader)    
        {
            restart:
            int typeCode = reader.ReadCompressedInteger();
            switch (typeCode)
            {
            // Encoded as a single code
            case (int)CorElementType.ELEMENT_TYPE_VOID:
            case (int)CorElementType.ELEMENT_TYPE_BOOLEAN:
            case (int)CorElementType.ELEMENT_TYPE_CHAR:
            case (int)CorElementType.ELEMENT_TYPE_I1:
            case (int)CorElementType.ELEMENT_TYPE_U1:
            case (int)CorElementType.ELEMENT_TYPE_I2:
            case (int)CorElementType.ELEMENT_TYPE_U2:
            case (int)CorElementType.ELEMENT_TYPE_I4:
            case (int)CorElementType.ELEMENT_TYPE_U4:
            case (int)CorElementType.ELEMENT_TYPE_I8:
            case (int)CorElementType.ELEMENT_TYPE_U8:
            case (int)CorElementType.ELEMENT_TYPE_R4:
            case (int)CorElementType.ELEMENT_TYPE_R8:
            case (int)CorElementType.ELEMENT_TYPE_STRING:
            case (int)CorElementType.ELEMENT_TYPE_TYPEDBYREF:
            case (int)CorElementType.ELEMENT_TYPE_I:
            case (int)CorElementType.ELEMENT_TYPE_U:
            case (int)CorElementType.ELEMENT_TYPE_OBJECT:
            return;

            // Encoded as a code followed by a type
            case (int)CorElementType.ELEMENT_TYPE_PTR:
            case (int)CorElementType.ELEMENT_TYPE_BYREF:
            case (int)CorElementType.ELEMENT_TYPE_SZARRAY:
            case (int)CorElementType.ELEMENT_TYPE_SENTINEL:
                goto restart;
            
            // Encoded as a code followed by a compressed int
            case (int)CorElementType.ELEMENT_TYPE_CLASS:
            case (int)CorElementType.ELEMENT_TYPE_VALUETYPE:
            case (int)CorElementType.ELEMENT_TYPE_CMOD_OPT:
            case (int)CorElementType.ELEMENT_TYPE_CMOD_REQD:
            case (int)CorElementType.ELEMENT_TYPE_VAR:
            case (int)CorElementType.ELEMENT_TYPE_MVAR:
                reader.ReadCompressedInteger();
                return;

            // MD Array skipping
            case (int)CorElementType.ELEMENT_TYPE_ARRAY:
                {
                    SkipType(ref reader);
                    int rank = reader.ReadCompressedInteger();
                    int numSizes = reader.ReadCompressedInteger();
                    for (int iSize = 0; iSize < numSizes; iSize++)
                        reader.ReadCompressedInteger();
                    int numLoBounds = reader.ReadCompressedInteger();
                    for (int iLoBound = 0; iLoBound < numLoBounds; iLoBound++)
                        reader.ReadCompressedInteger();
                    return;
                }

            // Generic inst handling
            case (int)CorElementType.ELEMENT_TYPE_GENERICINST:
                {
                    int elemType = reader.ReadCompressedInteger();
                    if ((elemType != (int)CorElementType.ELEMENT_TYPE_CLASS) &&
                        (elemType != (int)CorElementType.ELEMENT_TYPE_VALUETYPE))
                        throw new BadImageFormatException();
                    int typeDefOrRefEncoded = reader.ReadCompressedInteger();
                    int genericArgCount = reader.ReadCompressedInteger();
                    for (int iType = 0; iType < genericArgCount; iType++)
                        SkipType(ref reader);
                    return;
                }
            
            // Function pointer skip
            case (int)CorElementType.ELEMENT_TYPE_FNPTR:
                {
                    SignatureHeader header = reader.ReadSignatureHeader();
                    if (header.Kind != SignatureKind.Method)
                        throw new BadImageFormatException();
                    
                    int genericParameterCount = 0;
                    if (header.IsGeneric)
                        genericParameterCount = reader.ReadCompressedInteger();

                    int numParameters = reader.ReadCompressedInteger();
                    for (int iType = 0; iType < numParameters; iType++)
                        SkipType(ref reader);
                    return;
                }
            }
        }
    }
}
