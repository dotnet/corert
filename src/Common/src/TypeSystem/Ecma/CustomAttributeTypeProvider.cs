// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection.Metadata;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Ecma
{
    public struct CustomAttributeTypeProvider : ICustomAttributeTypeProvider<TypeDesc>
    {
        private EcmaModule _module;

        public CustomAttributeTypeProvider(EcmaModule module)
        {
            _module = module;
        }

        public TypeDesc GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            WellKnownType wkt;

            switch (typeCode)
            {
                case PrimitiveTypeCode.Boolean:
                    wkt = WellKnownType.Boolean;
                    break;
                case PrimitiveTypeCode.Byte:
                    wkt = WellKnownType.Byte;
                    break;
                case PrimitiveTypeCode.Char:
                    wkt = WellKnownType.Char;
                    break;
                case PrimitiveTypeCode.Double:
                    wkt = WellKnownType.Double;
                    break;
                case PrimitiveTypeCode.Int16:
                    wkt = WellKnownType.Int16;
                    break;
                case PrimitiveTypeCode.Int32:
                    wkt = WellKnownType.Int32;
                    break;
                case PrimitiveTypeCode.Int64:
                    wkt = WellKnownType.Int64;
                    break;
                case PrimitiveTypeCode.IntPtr:
                    wkt = WellKnownType.IntPtr;
                    break;
                case PrimitiveTypeCode.Object:
                    wkt = WellKnownType.Object;
                    break;
                case PrimitiveTypeCode.SByte:
                    wkt = WellKnownType.SByte;
                    break;
                case PrimitiveTypeCode.Single:
                    wkt = WellKnownType.Single;
                    break;
                case PrimitiveTypeCode.String:
                    wkt = WellKnownType.String;
                    break;
                case PrimitiveTypeCode.UInt16:
                    wkt = WellKnownType.UInt16;
                    break;
                case PrimitiveTypeCode.UInt32:
                    wkt = WellKnownType.UInt32;
                    break;
                case PrimitiveTypeCode.UInt64:
                    wkt = WellKnownType.UInt64;
                    break;
                case PrimitiveTypeCode.UIntPtr:
                    wkt = WellKnownType.UIntPtr;
                    break;
                case PrimitiveTypeCode.Void:
                    wkt = WellKnownType.Void;
                    break;
                case PrimitiveTypeCode.TypedReference:
                    throw new NotSupportedException();
                default:
                    throw new BadImageFormatException();
            }

            return _module.Context.GetWellKnownType(wkt);
        }

        public TypeDesc GetSystemType()
        {
            MetadataType systemType = _module.Context.SystemModule.GetType("System", "Type");
            return systemType;
        }

        public TypeDesc GetSZArrayType(TypeDesc elementType)
        {
            return elementType.MakeArrayType();
        }

        public TypeDesc GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            Debug.Assert(reader == _module.MetadataReader);
            return _module.GetType(handle);
        }

        public TypeDesc GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            Debug.Assert(reader == _module.MetadataReader);
            return _module.GetType(handle);
        }

        public TypeDesc GetTypeFromSpecification(MetadataReader reader, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            Debug.Assert(reader == _module.MetadataReader);
            return _module.GetType(handle);
        }

        public TypeDesc GetTypeFromSerializedName(string name)
        {
            if (name == null)
                return null;

            return _module.GetTypeByCustomAttributeTypeName(name);
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(TypeDesc type)
        {
            switch (type.UnderlyingType.Category)
            {
                case TypeFlags.Byte:
                    return PrimitiveTypeCode.Byte;
                case TypeFlags.SByte:
                    return PrimitiveTypeCode.SByte;
                case TypeFlags.UInt16:
                    return PrimitiveTypeCode.UInt16;
                case TypeFlags.Int16:
                    return PrimitiveTypeCode.Int16;
                case TypeFlags.UInt32:
                    return PrimitiveTypeCode.UInt32;
                case TypeFlags.Int32:
                    return PrimitiveTypeCode.Int32;
                case TypeFlags.UInt64:
                    return PrimitiveTypeCode.UInt64;
                case TypeFlags.Int64:
                    return PrimitiveTypeCode.Int64;
                default:
                    throw new BadImageFormatException();
            }
        }

        public bool IsSystemType(TypeDesc type)
        {
            var metadataType = type as MetadataType;
            return metadataType != null
                && metadataType.Name == "Type"
                && metadataType.Module == _module.Context.SystemModule
                && metadataType.Namespace == "System";
        }
    }
}