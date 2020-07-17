// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Internal.Reflection.Extensions.NonPortable;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime
{
    public static class EcmaMetadataHelpers
    {
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

        public static void SkipType(ref BlobReader reader)    
        {
            restart:
            CorElementType typeCode = (CorElementType)reader.ReadByte();
            switch (typeCode)
            {
                // Encoded as a single code
                case CorElementType.ELEMENT_TYPE_VOID:
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                case CorElementType.ELEMENT_TYPE_CHAR:
                case CorElementType.ELEMENT_TYPE_I1:
                case CorElementType.ELEMENT_TYPE_U1:
                case CorElementType.ELEMENT_TYPE_I2:
                case CorElementType.ELEMENT_TYPE_U2:
                case CorElementType.ELEMENT_TYPE_I4:
                case CorElementType.ELEMENT_TYPE_U4:
                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                case CorElementType.ELEMENT_TYPE_R4:
                case CorElementType.ELEMENT_TYPE_R8:
                case CorElementType.ELEMENT_TYPE_STRING:
                case CorElementType.ELEMENT_TYPE_TYPEDBYREF:
                case CorElementType.ELEMENT_TYPE_I:
                case CorElementType.ELEMENT_TYPE_U:
                case CorElementType.ELEMENT_TYPE_OBJECT:
                    return;

                // Encoded as a code followed by a type
                case CorElementType.ELEMENT_TYPE_PTR:
                case CorElementType.ELEMENT_TYPE_BYREF:
                case CorElementType.ELEMENT_TYPE_SZARRAY:
                case CorElementType.ELEMENT_TYPE_SENTINEL:
                    goto restart;
            
                // Encoded as a code followed by a compressed int
                case CorElementType.ELEMENT_TYPE_CLASS:
                case CorElementType.ELEMENT_TYPE_VALUETYPE:
                case CorElementType.ELEMENT_TYPE_CMOD_OPT:
                case CorElementType.ELEMENT_TYPE_CMOD_REQD:
                case CorElementType.ELEMENT_TYPE_VAR:
                case CorElementType.ELEMENT_TYPE_MVAR:
                    reader.ReadCompressedInteger();
                    return;

                // MD Array skipping
                case CorElementType.ELEMENT_TYPE_ARRAY:
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
                case CorElementType.ELEMENT_TYPE_GENERICINST:
                {
                    CorElementType elemType = (CorElementType)reader.ReadByte();
                    if ((elemType != CorElementType.ELEMENT_TYPE_CLASS) &&
                        (elemType != CorElementType.ELEMENT_TYPE_VALUETYPE))
                        throw new BadImageFormatException();
                    int typeDefOrRefEncoded = reader.ReadCompressedInteger();
                    int genericArgCount = reader.ReadCompressedInteger();
                    for (int iType = 0; iType < genericArgCount; iType++)
                        SkipType(ref reader);
                    return;
                }
            
                // Function pointer skip
                case CorElementType.ELEMENT_TYPE_FNPTR:
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

                default:
                    throw new BadImageFormatException();
            }
        }
    }
}
