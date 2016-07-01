using System;
using System.Diagnostics;
using System.Reflection;
using global::Internal.Metadata.NativeFormat;
using global::Internal.NativeFormat;
using global::Internal.Runtime.TypeLoader;
using Internal.Runtime.Augments;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    internal static class SigParsing
    {
        public static RuntimeTypeHandle GetTypeFromNativeLayoutSignature(ref NativeParser parser, uint offset)
        {
            IntPtr remainingSignature;
            RuntimeTypeHandle typeHandle;

            IntPtr signatureAddress = parser.Reader.OffsetToAddress(offset);
            bool success = TypeLoaderEnvironment.Instance.GetTypeFromSignatureAndContext(signatureAddress, null, null, out typeHandle, out remainingSignature);

            // Reset the parser to after the type
            parser = new NativeParser(parser.Reader, parser.Reader.AddressToOffset(remainingSignature));

            return typeHandle;
        }
    }
    
    public struct MethodSignatureComparer
    {
        /// <summary>
        /// Metadata reader corresponding to the method declaring type
        /// </summary>
        readonly MetadataReader _metadataReader;

        /// <summary>
        /// Method handle
        /// </summary>
        readonly MethodHandle _methodHandle;
        
        /// <summary>
        /// Method instance obtained from the method handle
        /// </summary>
        readonly Method _method;
        
        /// <summary>
        /// Method signature
        /// </summary>
        readonly MethodSignature _methodSignature;

        /// <summary>
        /// true = this is a static method
        /// </summary>
        readonly bool _isStatic;

        /// <summary>
        /// true = this is a generic method
        /// </summary>
        readonly bool _isGeneric;

        /// <summary>
        /// Construct a comparer between NativeFormat metadata methods and native layouts
        /// </summary>
        /// <param name="metadataReader">Metadata reader for the method declaring type</param>
        /// <param name="methodHandle">Handle of method to compare</param>
        public MethodSignatureComparer(
            MetadataReader metadataReader,
            MethodHandle methodHandle)
        {
            _metadataReader = metadataReader;
            _methodHandle = methodHandle;

            _method = methodHandle.GetMethod(metadataReader);

            _methodSignature = _method.Signature.GetMethodSignature(_metadataReader);
            _isGeneric = (_methodSignature.GenericParameterCount != 0);

            // Precalculate initial method attributes used in signature queries
            _isStatic = (_method.Flags & MethodAttributes.Static) != 0;
        }
        
        public bool IsMatchingNativeLayoutMethodNameAndSignature(string name, IntPtr signature)
        {
            return _method.Name.StringEquals(name, _metadataReader) &&
                IsMatchingNativeLayoutMethodSignature(signature);
        }

        public bool IsMatchingNativeLayoutMethodSignature(IntPtr signature)
        {
            NativeParser parser = GetNativeParserForSignature(signature);

            if (!CompareCallingConventions((MethodCallingConvention)parser.GetUnsigned()))
                return false;

            if (_isGeneric)
            {
                uint genericParamCount1 = parser.GetUnsigned();
                int genericParamCount2 = _methodSignature.GenericParameterCount;

                if (genericParamCount1 != genericParamCount2)
                    return false;
            }

            uint parameterCount = parser.GetUnsigned();

            if (!CompareTypeSigWithType(ref parser, _methodSignature.ReturnType
                .GetReturnTypeSignature(_metadataReader)
                .Type))
            {
                return false;
            }

            uint parameterIndexToMatch = 0;
            foreach (ParameterTypeSignatureHandle parameter in _methodSignature.Parameters)
            {
                if (parameterIndexToMatch >= parameterCount)
                {
                    // The metadata-defined _method has more parameters than the native layout
                    return false;
                }
                ParameterTypeSignature parameterSignature = parameter.GetParameterTypeSignature(_metadataReader);
                if (!CompareTypeSigWithType(ref parser, parameterSignature.Type))
                    return false;
                parameterIndexToMatch++;
            }

            // Make sure that all native layout parameters have been matched
            return parameterIndexToMatch == parameterCount;
        }

        /// <summary>
        /// Look up module containing given nativesignature and return the appropriate native parser.
        /// </summary>
        /// <param name="signature">Signature to look up</param>
        /// <returns>Native parser for the signature</param>
        internal static NativeParser GetNativeParserForSignature(IntPtr signature)
        {
            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(signature);
            NativeReader reader = TypeLoaderEnvironment.GetNativeReaderForBlob(moduleHandle, ReflectionMapBlob.NativeLayoutInfo);
            return new NativeParser(reader, reader.AddressToOffset(signature));
        }

        private bool CompareTypeSigWithType(ref NativeParser parser, Handle typeHandle)
        {
            while (typeHandle.HandleType == HandleType.TypeSpecification)
            {
                typeHandle = typeHandle
                    .ToTypeSpecificationHandle(_metadataReader)
                    .GetTypeSpecification(_metadataReader)
                    .Signature;
            }
            
            // startOffset lets us backtrack to the TypeSignatureKind for external types since the TypeLoader
            // expects to read it in.
            uint startOffset = parser.Offset;

            uint data;
            var typeSignatureKind = parser.GetTypeSignatureKind(out data);

            switch (typeSignatureKind)
            {
                case TypeSignatureKind.Lookback:
                    {
                        NativeParser lookbackParser = parser.GetLookbackParser(data);
                        return CompareTypeSigWithType(ref lookbackParser, typeHandle);
                    }

                case TypeSignatureKind.Modifier:
                    {
                        // Ensure the modifier kind (vector, pointer, byref) is the same
                        TypeModifierKind modifierKind = (TypeModifierKind)data;
                        switch (modifierKind)
                        {
                            case TypeModifierKind.Array:
                                if (typeHandle.HandleType == HandleType.SZArraySignature)
                                {
                                    return CompareTypeSigWithType(ref parser, typeHandle
                                        .ToSZArraySignatureHandle(_metadataReader)
                                        .GetSZArraySignature(_metadataReader)
                                        .ElementType);

                                }
                                return false;

                            case TypeModifierKind.ByRef:
                                if (typeHandle.HandleType == HandleType.ByReferenceSignature)
                                {
                                    return CompareTypeSigWithType(ref parser, typeHandle
                                        .ToByReferenceSignatureHandle(_metadataReader)
                                        .GetByReferenceSignature(_metadataReader)
                                        .Type);
                                }
                                return false;

                            case TypeModifierKind.Pointer:
                                if (typeHandle.HandleType == HandleType.PointerSignature)
                                {
                                    return CompareTypeSigWithType(ref parser, typeHandle
                                        .ToPointerSignatureHandle(_metadataReader)
                                        .GetPointerSignature(_metadataReader)
                                        .Type);
                                }
                                return false;

                            default:
                                Debug.Assert(null == "invalid type modifier kind");
                                return false;
                        }
                    }

                case TypeSignatureKind.Variable:
                    {
                        bool isMethodVar = (data & 0x1) == 1;
                        uint index = data >> 1;

                        if (isMethodVar)
                        {
                            if (typeHandle.HandleType == HandleType.MethodTypeVariableSignature)
                            {
                                return index == typeHandle
                                    .ToMethodTypeVariableSignatureHandle(_metadataReader)
                                    .GetMethodTypeVariableSignature(_metadataReader)
                                    .Number;
                            }
                        }
                        else
                        {
                            if (typeHandle.HandleType == HandleType.TypeVariableSignature)
                            {
                                return index == typeHandle
                                    .ToTypeVariableSignatureHandle(_metadataReader)
                                    .GetTypeVariableSignature(_metadataReader)
                                    .Number;
                            }
                        }

                        return false;
                    }

                case TypeSignatureKind.MultiDimArray:
                    {
                        if (typeHandle.HandleType != HandleType.ArraySignature)
                        {
                            return false;
                        }

                        ArraySignature sig = typeHandle
                            .ToArraySignatureHandle(_metadataReader)
                            .GetArraySignature(_metadataReader);

                        if (data != sig.Rank)
                            return false;

                        if (!CompareTypeSigWithType(ref parser, sig.ElementType))
                            return false;

                        uint boundCount1 = parser.GetUnsigned();
                        for (uint i = 0; i < boundCount1; i++)
                        {
                            parser.GetUnsigned();
                        }

                        uint lowerBoundCount1 = parser.GetUnsigned();

                        for (uint i = 0; i < lowerBoundCount1; i++)
                        {
                            parser.GetUnsigned();
                        }
                        break;
                    }

                case TypeSignatureKind.FunctionPointer:
                    {
                        // callingConvention is in data
                        uint argCount1 = parser.GetUnsigned();

                        for (uint i = 0; i < argCount1; i++)
                        {
                            if (!CompareTypeSigWithType(ref parser, typeHandle))
                                return false;
                        }
                        return false;
                    }

                case TypeSignatureKind.Instantiation:
                    {
                        if (typeHandle.HandleType != HandleType.TypeInstantiationSignature)
                        {
                            return false;
                        }
                        
                        TypeInstantiationSignature sig = typeHandle
                            .ToTypeInstantiationSignatureHandle(_metadataReader)
                            .GetTypeInstantiationSignature(_metadataReader);

                        if (!CompareTypeSigWithType(ref parser, sig.GenericType))
                        {
                            return false;
                        }

                        uint genericArgIndex = 0;
                        foreach (Handle genericArgumentTypeHandle in sig.GenericTypeArguments)
                        {
                            if (genericArgIndex >= data)
                            {
                                // The metadata generic has more parameters than the native layour
                                return false;
                            }
                            if (!CompareTypeSigWithType(ref parser, genericArgumentTypeHandle))
                            {
                                return false;
                            }
                            genericArgIndex++;
                        }
                        // Make sure all generic parameters have been matched
                        return genericArgIndex == data;
                    }

                case TypeSignatureKind.External:
                    {
                        RuntimeTypeHandle type2;
                        switch (typeHandle.HandleType)
                        {
                            case HandleType.TypeDefinition:
                                if (!TypeLoaderEnvironment.Instance.TryGetNamedTypeForMetadata(
                                    _metadataReader, typeHandle.ToTypeDefinitionHandle(_metadataReader), out type2))
                                {
                                    return false;
                                }
                                break;
                            
                            case HandleType.TypeReference:
                                if (!TypeLoaderEnvironment.TryGetNamedTypeForTypeReference(
                                    _metadataReader, typeHandle.ToTypeReferenceHandle(_metadataReader), out type2))
                                {
                                    return false;
                                }
                                break;
                            
                            default:
                                return false;
                        }

                        RuntimeTypeHandle type1 = SigParsing.GetTypeFromNativeLayoutSignature(ref parser, startOffset);
                        return type1.Equals(type2);
                    }

                default:
                    return false;
            }
            return true;
        }

        private bool CompareCallingConventions(MethodCallingConvention callingConvention)
        {
            return (callingConvention.HasFlag(MethodCallingConvention.Static) == _isStatic) &&
                (callingConvention.HasFlag(MethodCallingConvention.Generic) == _isGeneric);
        }

        private static bool CanGetTypeHandle(Type type)
        {
            if (type.HasElementType)
            {
                return CanGetTypeHandle(type.GetElementType());
            }
            else if (type.IsConstructedGenericType)
            {
                foreach (var typeArg in type.GenericTypeArguments)
                {
                    if (!CanGetTypeHandle(typeArg))
                    {
                        return false;
                    }
                }
            }
            else if (type.IsGenericParameter)
            {
                return false;
            }

            return true;
        }
    }
}
