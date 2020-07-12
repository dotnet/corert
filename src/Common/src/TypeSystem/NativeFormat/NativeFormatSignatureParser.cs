// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using Internal.Metadata.NativeFormat;
using System.Runtime.InteropServices;

using Internal.TypeSystem;

namespace Internal.TypeSystem.NativeFormat
{
    public struct NativeFormatSignatureParser
    {
        private NativeFormatMetadataUnit _metadataUnit;
        private Handle _signatureHandle;
        private MetadataReader _metadataReader;

        // TODO
        // bool _hasModifiers;

        public NativeFormatSignatureParser(NativeFormatMetadataUnit metadataUnit, Handle signatureHandle, MetadataReader metadataReader)
        {
            _metadataUnit = metadataUnit;
            _signatureHandle = signatureHandle;
            _metadataReader = metadataReader;
            // _hasModifiers = false;
        }

        public bool IsFieldSignature
        {
            get
            {
                return _signatureHandle.HandleType == HandleType.FieldSignature;
            }
        }

        public MethodSignature ParseMethodSignature()
        {
            var methodSignature = _metadataReader.GetMethodSignature(_signatureHandle.ToMethodSignatureHandle(_metadataReader));

            MethodSignatureFlags flags = 0;

            if ((methodSignature.CallingConvention & System.Reflection.CallingConventions.HasThis) == 0)
                flags |= MethodSignatureFlags.Static;

            int arity = methodSignature.GenericParameterCount;

            var parameterSignatureArray = methodSignature.Parameters;

            int count = parameterSignatureArray.Count;

            TypeDesc returnType = _metadataUnit.GetType(methodSignature.ReturnType);
            TypeDesc[] parameters;

            if (count > 0)
            {
                // Get all of the parameters.
                parameters = new TypeDesc[count];
                int i = 0;
                foreach (Handle parameterHandle in parameterSignatureArray)
                {
                    parameters[i] = _metadataUnit.GetType(parameterHandle);
                    i++;
                }
            }
            else
            {
                parameters = TypeDesc.EmptyTypes;
            }

            return new MethodSignature(flags, arity, returnType, parameters);
        }

        public TypeDesc ParseFieldSignature()
        {
            if (_signatureHandle.HandleType != HandleType.FieldSignature)
                throw new BadImageFormatException();

            var fieldSignature = _metadataReader.GetFieldSignature(_signatureHandle.ToFieldSignatureHandle(_metadataReader));
            return _metadataUnit.GetType(fieldSignature.Type);
        }

        /// <summary>
        /// Use for handle kinds, TypeInstantiationSignature, TypeSpecification, SZArraySignature, ArraySignature, PointerSignature, ByReferenceSignature, TypeVariableSignature, MethodTypeVariableSignature
        /// </summary>
        /// <returns></returns>
        public TypeDesc ParseTypeSignature()
        {
            switch (_signatureHandle.HandleType)
            {
                case HandleType.TypeSpecification:
                    {
                        var typeSpec = _metadataReader.GetTypeSpecification(_signatureHandle.ToTypeSpecificationHandle(_metadataReader));
                        return _metadataUnit.GetType(typeSpec.Signature);
                    }

                case HandleType.TypeInstantiationSignature:
                    {
                        var typeInstantiationSignature = _metadataReader.GetTypeInstantiationSignature(_signatureHandle.ToTypeInstantiationSignatureHandle(_metadataReader));
                        var openType = (MetadataType)_metadataUnit.GetType(typeInstantiationSignature.GenericType);
                        var typeArguments = typeInstantiationSignature.GenericTypeArguments;
                        TypeDesc[] instantiationArguments = new TypeDesc[typeArguments.Count];
                        int i = 0;
                        foreach (Handle typeArgument in typeArguments)
                        {
                            instantiationArguments[i] = _metadataUnit.GetType(typeArgument);
                            i++;
                        }
                        return _metadataUnit.Context.GetInstantiatedType(openType, new Instantiation(instantiationArguments));
                    }

                case HandleType.SZArraySignature:
                    {
                        var szArraySignature = _metadataReader.GetSZArraySignature(_signatureHandle.ToSZArraySignatureHandle(_metadataReader));
                        return _metadataUnit.Context.GetArrayType(_metadataUnit.GetType(szArraySignature.ElementType));
                    }

                case HandleType.ArraySignature:
                    {
                        var arraySignature = _metadataReader.GetArraySignature(_signatureHandle.ToArraySignatureHandle(_metadataReader));
                        return _metadataUnit.Context.GetArrayType(_metadataUnit.GetType(arraySignature.ElementType), arraySignature.Rank);
                    }

                case HandleType.PointerSignature:
                    {
                        var pointerSignature = _metadataReader.GetPointerSignature(_signatureHandle.ToPointerSignatureHandle(_metadataReader));
                        return _metadataUnit.Context.GetPointerType(_metadataUnit.GetType(pointerSignature.Type));
                    }

                case HandleType.ByReferenceSignature:
                    {
                        var byReferenceSignature = _metadataReader.GetByReferenceSignature(_signatureHandle.ToByReferenceSignatureHandle(_metadataReader));
                        return _metadataUnit.Context.GetByRefType(_metadataUnit.GetType(byReferenceSignature.Type));
                    }

                case HandleType.TypeVariableSignature:
                    {
                        var typeVariableSignature = _metadataReader.GetTypeVariableSignature(_signatureHandle.ToTypeVariableSignatureHandle(_metadataReader));
                        return _metadataUnit.Context.GetSignatureVariable(typeVariableSignature.Number, false);
                    }

                case HandleType.MethodTypeVariableSignature:
                    {
                        var methodVariableSignature = _metadataReader.GetMethodTypeVariableSignature(_signatureHandle.ToMethodTypeVariableSignatureHandle(_metadataReader));
                        return _metadataUnit.Context.GetSignatureVariable(methodVariableSignature.Number, true);
                    }

                case HandleType.FunctionPointerSignature:
                    {
                        var functionPointerSignature = _metadataReader.GetFunctionPointerSignature(_signatureHandle.ToFunctionPointerSignatureHandle(_metadataReader));
                        NativeFormatSignatureParser methodSigParser = new NativeFormatSignatureParser(_metadataUnit, functionPointerSignature.Signature, _metadataReader);
                        return _metadataUnit.Context.GetFunctionPointerType(methodSigParser.ParseMethodSignature());
                    }
                default:
                    throw new BadImageFormatException();
            }
        }
    }
}
