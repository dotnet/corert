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
        private static Type s_objectType = typeof(object);
        private static Type s_intPtrType = typeof(IntPtr);
        private static Type s_int64Type = typeof(long);
        private static Type s_int32Type = typeof(int);
        private static Type s_int16Type = typeof(short);
        private static Type s_int8Type = typeof(sbyte);
        private static Type s_uintPtrType = typeof(UIntPtr);
        private static Type s_uint64Type = typeof(ulong);
        private static Type s_uint32Type = typeof(uint);
        private static Type s_uint16Type = typeof(ushort);
        private static Type s_uint8Type = typeof(byte);
        private static Type s_charType = typeof(char);
        private static Type s_floatType = typeof(float);
        private static Type s_doubleType = typeof(double);
        private static Type s_boolType = typeof(bool);
        private static Type s_stringType = typeof(string);
        private static Type s_voidType = typeof(void);

        public static PrimitiveTypeCode GetPrimitiveTypeCode(this Type type)
        {
            if (type == s_objectType)
                return PrimitiveTypeCode.Object;
            else if (type == s_boolType)
                return PrimitiveTypeCode.Boolean;
            else if (type == s_charType)
                return PrimitiveTypeCode.Char;
            else if (type == s_doubleType)
                return PrimitiveTypeCode.Double;
            else if (type == s_floatType)
                return PrimitiveTypeCode.Single;
            else if (type == s_int16Type)
                return PrimitiveTypeCode.Int16;
            else if (type == s_int32Type)
                return PrimitiveTypeCode.Int32;
            else if (type == s_int64Type)
                return PrimitiveTypeCode.Int64;
            else if (type == s_int8Type)
                return PrimitiveTypeCode.SByte;
            else if (type == s_uint16Type)
                return PrimitiveTypeCode.UInt16;
            else if (type == s_uint32Type)
                return PrimitiveTypeCode.UInt32;
            else if (type == s_uint64Type)
                return PrimitiveTypeCode.UInt64;
            else if (type == s_uint8Type)
                return PrimitiveTypeCode.Byte;
            else if (type == s_intPtrType)
                return PrimitiveTypeCode.IntPtr;
            else if (type == s_uintPtrType)
                return PrimitiveTypeCode.UIntPtr;
            else if (type == s_stringType)
                return PrimitiveTypeCode.String;
            else if (type == s_voidType)
                return PrimitiveTypeCode.Void;
            
            throw new ArgumentException();
        }

        public static Type GetRuntimeType(this PrimitiveTypeCode primitiveCode)
        {
            switch(primitiveCode)
            {
                case PrimitiveTypeCode.Boolean:
                    return s_boolType;
                case PrimitiveTypeCode.Byte:
                    return s_uint8Type;
                case PrimitiveTypeCode.Char:
                    return s_charType;
                case PrimitiveTypeCode.Double:
                    return s_doubleType;
                case PrimitiveTypeCode.Int16:
                    return s_int16Type;
                case PrimitiveTypeCode.Int32:
                    return s_int32Type;
                case PrimitiveTypeCode.Int64:
                    return s_int64Type;
                case PrimitiveTypeCode.IntPtr:
                    return s_intPtrType;
                case PrimitiveTypeCode.Object:
                    return s_objectType;
                case PrimitiveTypeCode.SByte:
                    return s_int8Type;
                case PrimitiveTypeCode.Single:
                    return s_floatType;
                case PrimitiveTypeCode.String:
                    return s_stringType;
                case PrimitiveTypeCode.TypedReference:
                    throw new PlatformNotSupportedException();
                case PrimitiveTypeCode.UInt16:
                    return s_uint16Type;
                case PrimitiveTypeCode.UInt32:
                    return s_uint32Type;
                case PrimitiveTypeCode.UInt64:
                    return s_uint64Type;
                case PrimitiveTypeCode.UIntPtr:
                    return s_uintPtrType;
                case PrimitiveTypeCode.Void:
                    return s_voidType;
            }

            throw new BadImageFormatException();
        }
    }
    class ReflectionTypeProvider : ICustomAttributeTypeProvider<RuntimeTypeInfo>, ISZArrayTypeProvider<RuntimeTypeInfo>, ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>
    {
        private static RuntimeTypeInfo s_systemType = (RuntimeTypeInfo)typeof(Type).GetTypeInfo();
        private static RuntimeTypeInfo s_intPtrType = (RuntimeTypeInfo)typeof(IntPtr).GetTypeInfo();

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

        public RuntimeTypeInfo GetSystemType()
        {
            return s_systemType;
        }

        public bool IsSystemType(RuntimeTypeInfo type)
        {
            return Object.ReferenceEquals(s_systemType, type);
        }

        public RuntimeTypeInfo GetTypeFromSerializedName(string name)
        {
            RuntimeTypeInfo result = (RuntimeTypeInfo)Type.GetType(name, _throwOnError);
            if (result == null)
                ExceptionResult = new TypeLoadException();

            return result;
        }

        public PrimitiveTypeCode GetUnderlyingEnumType(RuntimeTypeInfo type)
        {
            Debug.Assert(type.IsEnum);
            return type.GetEnumUnderlyingType().GetPrimitiveTypeCode();
        }

        public RuntimeTypeInfo GetPrimitiveType(PrimitiveTypeCode primitiveCode)
        {
            return (RuntimeTypeInfo)primitiveCode.GetRuntimeType().GetTypeInfo();
        }

        public RuntimeTypeInfo GetSZArrayType(RuntimeTypeInfo elementType)
        {
            if (elementType == null)
            {
                ExceptionResult = new BadImageFormatException();
                return null;
            }
            
            return (RuntimeTypeInfo)elementType.MakeArrayType().GetTypeInfo();
        }

        public RuntimeTypeInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
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

        public RuntimeTypeInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
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

        public RuntimeTypeInfo GetTypeFromSpecification(MetadataReader reader, TypeContext typeContext, TypeSpecificationHandle handle, byte rawTypeKind)
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

        // ISignatureTypeProvider
        public RuntimeTypeInfo GetFunctionPointerType(MethodSignature<RuntimeTypeInfo> signature)
        {
            return s_intPtrType;
        }

        public RuntimeTypeInfo GetGenericTypeParameter(TypeContext typeContext, int parameter)
        {
            if ((typeContext.GenericTypeArguments == null) ||
                (typeContext.GenericTypeArguments.Length < parameter) ||
                (parameter < 0))
                ExceptionResult = new BadImageFormatException();

            return typeContext.GenericTypeArguments[parameter];
        }

        public RuntimeTypeInfo GetGenericMethodParameter(TypeContext typeContext, int parameter)
        {
            if ((typeContext.GenericMethodArguments == null) ||
                (typeContext.GenericMethodArguments.Length < parameter) ||
                (parameter < 0))
                ExceptionResult = new BadImageFormatException();

            return typeContext.GenericMethodArguments[parameter];
        }
        
        public RuntimeTypeInfo GetModifiedType(RuntimeTypeInfo modifier, RuntimeTypeInfo unmodifiedType, bool isRequired)
        {
            // Reflection doesn't really model custom modifiers...
            return unmodifiedType;
        }

        public RuntimeTypeInfo GetPinnedType(RuntimeTypeInfo elementType)
        {
            // Reflection doesn't model pinned types
            return elementType;
        }

        // IConstructedTypeProvider
        public RuntimeTypeInfo GetGenericInstantiation(RuntimeTypeInfo genericType, ImmutableArray<RuntimeTypeInfo> typeArguments)
        {
            Type[] typeArgumentsAsType = new Type[typeArguments.Length];
            for (int i = 0 ; i < typeArgumentsAsType.Length; i++)
            {
                typeArgumentsAsType[i] = typeArguments[i].AsType();
            }

            return (RuntimeTypeInfo)genericType.MakeGenericType(typeArgumentsAsType).GetTypeInfo();
        }

        public RuntimeTypeInfo GetArrayType(RuntimeTypeInfo elementType, ArrayShape shape)
        {
            if ((shape.Rank < 1) || (shape.Rank > 32))
                ExceptionResult = new BadImageFormatException();

            return elementType.GetMultiDimArrayType(shape.Rank);
        }

        public RuntimeTypeInfo GetByReferenceType(RuntimeTypeInfo elementType)
        {
            return elementType.GetByRefType();
        }

        public RuntimeTypeInfo GetPointerType(RuntimeTypeInfo elementType)
        {
            return elementType.GetPointerType();
        }
    }
}