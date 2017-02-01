// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Reflection.Metadata;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

namespace System.Reflection.Runtime
{
    static class ReflectionTypeProviderHelpers
    {
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
    }
    class ReflectionTypeProvider : ICustomAttributeTypeProvider<RuntimeTypeInfo>, ISZArrayTypeProvider<RuntimeTypeInfo>, ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>
    {
        private static readonly RuntimeTypeInfo s_systemType = (RuntimeTypeInfo)CommonRuntimeTypes.Type.GetTypeInfo();
        private static readonly RuntimeTypeInfo s_intPtrType = (RuntimeTypeInfo)CommonRuntimeTypes.IntPtr.GetTypeInfo();

        private bool _throwOnError;
        private Exception _exceptionResult;
        public bool ExceptionOccurred { get {return _exceptionResult != null; } }
        public Exception ExceptionResult
        {
            get
            {
                return _exceptionResult;
            }
            set
            {
                if (_exceptionResult == null)
                {
                    _exceptionResult = value;

                    if (_exceptionResult != null)
                        throw _exceptionResult;
                }
            }
        }

        public ReflectionTypeProvider(bool throwOnError)
        {
            _throwOnError = throwOnError;
        }

        RuntimeTypeInfo ICustomAttributeTypeProvider<RuntimeTypeInfo>.GetSystemType()
        {
            return s_systemType;
        }

        bool ICustomAttributeTypeProvider<RuntimeTypeInfo>.IsSystemType(RuntimeTypeInfo type)
        {
            return Object.ReferenceEquals(s_systemType, type);
        }

        RuntimeTypeInfo ICustomAttributeTypeProvider<RuntimeTypeInfo>.GetTypeFromSerializedName(string name)
        {
            RuntimeTypeInfo result = (RuntimeTypeInfo)Type.GetType(name, _throwOnError);
            if (result == null)
                ExceptionResult = new TypeLoadException();

            return result;
        }

        PrimitiveTypeCode ICustomAttributeTypeProvider<RuntimeTypeInfo>.GetUnderlyingEnumType(RuntimeTypeInfo type)
        {
            Debug.Assert(type.IsEnum);
            return type.GetEnumUnderlyingType().GetPrimitiveTypeCode();
        }

        RuntimeTypeInfo ISimpleTypeProvider<RuntimeTypeInfo>.GetPrimitiveType(PrimitiveTypeCode primitiveCode)
        {
            return (RuntimeTypeInfo)primitiveCode.GetRuntimeType().GetTypeInfo();
        }

        RuntimeTypeInfo ISZArrayTypeProvider<RuntimeTypeInfo>.GetSZArrayType(RuntimeTypeInfo elementType)
        {
            if (elementType == null)
            {
                ExceptionResult = new BadImageFormatException();
                return null;
            }
            
            return (RuntimeTypeInfo)elementType.MakeArrayType().GetTypeInfo();
        }

        RuntimeTypeInfo ISimpleTypeProvider<RuntimeTypeInfo>.GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            // raw Type Kind is either 0, ELEMENT_TYPE_CLASS, or ELEMENT_TYPE_VALUETYPE
            Exception exception = null;
            RuntimeTypeInfo result = ((Handle)handle).TryResolve(reader, default(TypeContext), ref exception);
            if (result != null)
                return result;
            else
            {
                ExceptionResult = exception;
                return null;
            }
        }

        RuntimeTypeInfo ISimpleTypeProvider<RuntimeTypeInfo>.GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            // raw Type Kind is either 0, ELEMENT_TYPE_CLASS, or ELEMENT_TYPE_VALUETYPE
            Exception exception = null;
            RuntimeTypeInfo result = ((Handle)handle).TryResolve(reader, default(TypeContext), ref exception);
            if (result != null)
                return result;
            else
            {
                ExceptionResult = exception;
                return null;
            }
        }

        // ISignatureTypeProvider
        RuntimeTypeInfo ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>.GetTypeFromSpecification(MetadataReader reader, TypeContext typeContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            // raw Type Kind is either 0, ELEMENT_TYPE_CLASS, or ELEMENT_TYPE_VALUETYPE
            Exception exception = null;
            RuntimeTypeInfo result = ((Handle)handle).TryResolve(reader, typeContext, ref exception);
            if (result != null)
                return result;
            else
            {
                ExceptionResult = exception;
                return null;
            }
        }

        RuntimeTypeInfo ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>.GetFunctionPointerType(MethodSignature<RuntimeTypeInfo> signature)
        {
            return s_intPtrType;
        }

        RuntimeTypeInfo ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>.GetGenericTypeParameter(TypeContext typeContext, int parameter)
        {
            if ((typeContext.GenericTypeArguments == null) ||
                (typeContext.GenericTypeArguments.Length < parameter) ||
                (parameter < 0))
                ExceptionResult = new BadImageFormatException();

            return typeContext.GenericTypeArguments[parameter];
        }

        RuntimeTypeInfo ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>.GetGenericMethodParameter(TypeContext typeContext, int parameter)
        {
            if ((typeContext.GenericMethodArguments == null) ||
                (typeContext.GenericMethodArguments.Length < parameter) ||
                (parameter < 0))
                ExceptionResult = new BadImageFormatException();

            return typeContext.GenericMethodArguments[parameter];
        }
        
        RuntimeTypeInfo ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>.GetModifiedType(RuntimeTypeInfo modifier, RuntimeTypeInfo unmodifiedType, bool isRequired)
        {
            // Reflection doesn't really model custom modifiers...
            return unmodifiedType;
        }

        RuntimeTypeInfo ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>.GetPinnedType(RuntimeTypeInfo elementType)
        {
            // Reflection doesn't model pinned types
            return elementType;
        }

        // IConstructedTypeProvider
        RuntimeTypeInfo IConstructedTypeProvider<RuntimeTypeInfo>.GetGenericInstantiation(RuntimeTypeInfo genericType, ImmutableArray<RuntimeTypeInfo> typeArguments)
        {
            Type[] typeArgumentsAsType = new Type[typeArguments.Length];
            for (int i = 0 ; i < typeArgumentsAsType.Length; i++)
            {
                typeArgumentsAsType[i] = typeArguments[i].AsType();
            }

            return (RuntimeTypeInfo)genericType.MakeGenericType(typeArgumentsAsType).GetTypeInfo();
        }

        RuntimeTypeInfo IConstructedTypeProvider<RuntimeTypeInfo>.GetArrayType(RuntimeTypeInfo elementType, ArrayShape shape)
        {
            if ((shape.Rank < 1) || (shape.Rank > 32))
                ExceptionResult = new BadImageFormatException();

            return elementType.GetMultiDimArrayType(shape.Rank);
        }

        RuntimeTypeInfo IConstructedTypeProvider<RuntimeTypeInfo>.GetByReferenceType(RuntimeTypeInfo elementType)
        {
            return elementType.GetByRefType();
        }

        RuntimeTypeInfo IConstructedTypeProvider<RuntimeTypeInfo>.GetPointerType(RuntimeTypeInfo elementType)
        {
            return elementType.GetPointerType();
        }
    }
}