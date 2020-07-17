// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    internal sealed class ReflectionTypeProvider : ICustomAttributeTypeProvider<RuntimeTypeInfo>, ISZArrayTypeProvider<RuntimeTypeInfo>, ISignatureTypeProvider<RuntimeTypeInfo, TypeContext>
    {
        private readonly bool _throwOnError;
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
            return CommonRuntimeTypes.Type.CastToRuntimeTypeInfo();
        }

        bool ICustomAttributeTypeProvider<RuntimeTypeInfo>.IsSystemType(RuntimeTypeInfo type)
        {
            return CommonRuntimeTypes.Type.Equals(type);
        }

        RuntimeTypeInfo ICustomAttributeTypeProvider<RuntimeTypeInfo>.GetTypeFromSerializedName(string name)
        {
            RuntimeTypeInfo result = Type.GetType(name, _throwOnError).CastToRuntimeTypeInfo();
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
            return primitiveCode.GetRuntimeType().CastToRuntimeTypeInfo();
        }

        RuntimeTypeInfo ISZArrayTypeProvider<RuntimeTypeInfo>.GetSZArrayType(RuntimeTypeInfo elementType)
        {
            if (elementType == null)
            {
                ExceptionResult = new BadImageFormatException();
                return null;
            }
            
            return elementType.MakeArrayType().CastToRuntimeTypeInfo();
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
            return CommonRuntimeTypes.IntPtr.CastToRuntimeTypeInfo();
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
            RuntimeTypeInfo[] typeArgumentsArray = new RuntimeTypeInfo[typeArguments.Length];
            typeArguments.CopyTo(typeArgumentsArray);
            return genericType.GetConstructedGenericType(typeArgumentsArray);
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
