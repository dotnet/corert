// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Runtime.General
{
    //
    // Collect various metadata reading tasks for better chunking...
    //
    public static class EcmaMetadataReaderExtensions
    {
        //
        // Used to split methods between DeclaredMethods and DeclaredConstructors.
        //
        public static bool IsConstructor(this MethodDefinitionHandle methodHandle, MetadataReader reader)
        {
            MethodDefinition method = reader.GetMethodDefinition(methodHandle);
            return EcmaMetadataHelpers.IsConstructor(ref method, reader);
        }

        public static PrimitiveTypeCode GetPrimitiveTypeCode(this Type type)
        {
            if (type == CommonRuntimeTypes.Object)
                return PrimitiveTypeCode.Object;
            else if (type == CommonRuntimeTypes.Boolean)
                return PrimitiveTypeCode.Boolean;
            else if (type == CommonRuntimeTypes.Char)
                return PrimitiveTypeCode.Char;
            else if (type == CommonRuntimeTypes.Double)
                return PrimitiveTypeCode.Double;
            else if (type == CommonRuntimeTypes.Single)
                return PrimitiveTypeCode.Single;
            else if (type == CommonRuntimeTypes.Int16)
                return PrimitiveTypeCode.Int16;
            else if (type == CommonRuntimeTypes.Int32)
                return PrimitiveTypeCode.Int32;
            else if (type == CommonRuntimeTypes.Int64)
                return PrimitiveTypeCode.Int64;
            else if (type == CommonRuntimeTypes.SByte)
                return PrimitiveTypeCode.SByte;
            else if (type == CommonRuntimeTypes.UInt16)
                return PrimitiveTypeCode.UInt16;
            else if (type == CommonRuntimeTypes.UInt32)
                return PrimitiveTypeCode.UInt32;
            else if (type == CommonRuntimeTypes.UInt64)
                return PrimitiveTypeCode.UInt64;
            else if (type == CommonRuntimeTypes.Byte)
                return PrimitiveTypeCode.Byte;
            else if (type == CommonRuntimeTypes.IntPtr)
                return PrimitiveTypeCode.IntPtr;
            else if (type == CommonRuntimeTypes.UIntPtr)
                return PrimitiveTypeCode.UIntPtr;
            else if (type == CommonRuntimeTypes.String)
                return PrimitiveTypeCode.String;
            else if (type == CommonRuntimeTypes.Void)
                return PrimitiveTypeCode.Void;
            
            throw new ArgumentException();
        }

        public static Type GetRuntimeType(this PrimitiveTypeCode primitiveCode)
        {
            switch(primitiveCode)
            {
                case PrimitiveTypeCode.Boolean:
                    return CommonRuntimeTypes.Boolean;
                case PrimitiveTypeCode.Byte:
                    return CommonRuntimeTypes.Byte;
                case PrimitiveTypeCode.Char:
                    return CommonRuntimeTypes.Char;
                case PrimitiveTypeCode.Double:
                    return CommonRuntimeTypes.Double;
                case PrimitiveTypeCode.Int16:
                    return CommonRuntimeTypes.Int16;
                case PrimitiveTypeCode.Int32:
                    return CommonRuntimeTypes.Int32;
                case PrimitiveTypeCode.Int64:
                    return CommonRuntimeTypes.Int64;
                case PrimitiveTypeCode.IntPtr:
                    return CommonRuntimeTypes.IntPtr;
                case PrimitiveTypeCode.Object:
                    return CommonRuntimeTypes.Object;
                case PrimitiveTypeCode.SByte:
                    return CommonRuntimeTypes.SByte;
                case PrimitiveTypeCode.Single:
                    return CommonRuntimeTypes.Single;
                case PrimitiveTypeCode.String:
                    return CommonRuntimeTypes.String;
                case PrimitiveTypeCode.TypedReference:
                    throw new PlatformNotSupportedException();
                case PrimitiveTypeCode.UInt16:
                    return CommonRuntimeTypes.UInt16;
                case PrimitiveTypeCode.UInt32:
                    return CommonRuntimeTypes.UInt32;
                case PrimitiveTypeCode.UInt64:
                    return CommonRuntimeTypes.UInt64;
                case PrimitiveTypeCode.UIntPtr:
                    return CommonRuntimeTypes.UIntPtr;
                case PrimitiveTypeCode.Void:
                    return CommonRuntimeTypes.Void;
            }

            throw new BadImageFormatException();
        }

        public static object ParseConstantValue(this ConstantHandle constantHandle, MetadataReader metadataReader)
        {
            if (constantHandle.IsNil)
                throw new BadImageFormatException();

            Constant constantValue = metadataReader.GetConstant(constantHandle);

            if (constantValue.Value.IsNil)
                throw new BadImageFormatException();

            BlobReader reader = metadataReader.GetBlobReader(constantValue.Value);

            switch (constantValue.TypeCode)
            {
                case ConstantTypeCode.Boolean:
                    return reader.ReadBoolean();

                case ConstantTypeCode.Char:
                    return reader.ReadChar();

                case ConstantTypeCode.SByte:
                    return reader.ReadSByte();

                case ConstantTypeCode.Int16:
                    return reader.ReadInt16();

                case ConstantTypeCode.Int32:
                    return reader.ReadInt32();

                case ConstantTypeCode.Int64:
                    return reader.ReadInt64();

                case ConstantTypeCode.Byte:
                    return reader.ReadByte();

                case ConstantTypeCode.UInt16:
                    return reader.ReadUInt16();

                case ConstantTypeCode.UInt32:
                    return reader.ReadUInt32();

                case ConstantTypeCode.UInt64:
                    return reader.ReadUInt64();

                case ConstantTypeCode.Single:
                    return reader.ReadSingle();

                case ConstantTypeCode.Double:
                    return reader.ReadDouble();

                case ConstantTypeCode.String:
                    return reader.ReadUTF16(reader.Length);

                case ConstantTypeCode.NullReference:
                    // Partition II section 22.9:
                    // The encoding of Type for the nullref value is ELEMENT_TYPE_CLASS with a Value of a 4-byte zero.
                    // Unlike uses of ELEMENT_TYPE_CLASS in signatures, this one is not followed by a type token.
                    if (reader.ReadUInt32() == 0)
                    {
                        return null;
                    }

                    break;
            }

            throw new BadImageFormatException();
        }

        public static bool IsCustomAttributeOfType(this CustomAttributeHandle handle, MetadataReader reader, string ns, string name)
        {
            CustomAttribute attribute = reader.GetCustomAttribute(handle);
            EcmaMetadataHelpers.GetAttributeTypeDefRefOrSpecHandle(reader, attribute.Constructor, out EntityHandle typeDefOrRefOrSpec);

            switch (typeDefOrRefOrSpec.Kind)
            {
                case HandleKind.TypeReference:
                    TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)typeDefOrRefOrSpec);
                    HandleKind handleType = typeRef.ResolutionScope.Kind;

                    if (handleType == HandleKind.TypeReference || handleType == HandleKind.TypeDefinition)
                    {
                        // Nested type
                        return false;
                    }

                    return reader.StringComparer.Equals(typeRef.Name, name)
                        && reader.StringComparer.Equals(typeRef.Namespace, name);

                case HandleKind.TypeDefinition:
                    TypeDefinition typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)typeDefOrRefOrSpec);

                    if (EcmaMetadataHelpers.IsNested(typeDef.Attributes))
                    {
                        // Nested type
                        return false;
                    }

                    return reader.StringComparer.Equals(typeDef.Name, name)
                        && reader.StringComparer.Equals(typeDef.Namespace, name);

                case HandleKind.TypeSpecification:
                    // Generic attribute
                    return false;

                default:
                    // unsupported metadata
                    throw new BadImageFormatException();
            }
        }
    }
}
