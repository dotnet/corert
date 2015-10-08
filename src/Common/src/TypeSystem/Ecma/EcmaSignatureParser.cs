// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public struct EcmaSignatureParser
    {
        EcmaModule _module;
        BlobReader _reader;

        // TODO
        // bool _hasModifiers;

        public EcmaSignatureParser(EcmaModule module, BlobReader reader)
        {
            _module = module;
            _reader = reader;

            // _hasModifiers = false;
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _module.Context.GetWellKnownType(wellKnownType);
        }

        private TypeDesc ParseType(SignatureTypeCode typeCode)
        {
            // Switch on the type.
            switch (typeCode)
            {
                case SignatureTypeCode.Void:
                    return GetWellKnownType(WellKnownType.Void);
                case SignatureTypeCode.Boolean:
                    return GetWellKnownType(WellKnownType.Boolean);
                case SignatureTypeCode.SByte:
                    return GetWellKnownType(WellKnownType.SByte);
                case SignatureTypeCode.Byte:
                    return GetWellKnownType(WellKnownType.Byte);
                case SignatureTypeCode.Int16:
                    return GetWellKnownType(WellKnownType.Int16);
                case SignatureTypeCode.UInt16:
                    return GetWellKnownType(WellKnownType.UInt16);
                case SignatureTypeCode.Int32:
                    return GetWellKnownType(WellKnownType.Int32);
                case SignatureTypeCode.UInt32:
                    return GetWellKnownType(WellKnownType.UInt32);
                case SignatureTypeCode.Int64:
                    return GetWellKnownType(WellKnownType.Int64);
                case SignatureTypeCode.UInt64:
                    return GetWellKnownType(WellKnownType.UInt64);
                case SignatureTypeCode.Single:
                    return GetWellKnownType(WellKnownType.Single);
                case SignatureTypeCode.Double:
                    return GetWellKnownType(WellKnownType.Double);
                case SignatureTypeCode.Char:
                    return GetWellKnownType(WellKnownType.Char);
                case SignatureTypeCode.String:
                    return GetWellKnownType(WellKnownType.String);
                case SignatureTypeCode.IntPtr:
                    return GetWellKnownType(WellKnownType.IntPtr);
                case SignatureTypeCode.UIntPtr:
                    return GetWellKnownType(WellKnownType.UIntPtr);
                case SignatureTypeCode.Object:
                    return GetWellKnownType(WellKnownType.Object);
                case SignatureTypeCode.TypeHandle:
                    return _module.GetType(_reader.ReadTypeHandle());
                case SignatureTypeCode.SZArray:
                    return _module.Context.GetArrayType(ParseType());
                case SignatureTypeCode.Array:
                    {
                        var elementType = ParseType();
                        var rank = _reader.ReadCompressedInteger();
 
                        // TODO: Bounds for multi-dimmensional arrays
                        var boundsCount = _reader.ReadCompressedInteger();
                        for (int i = 0; i < boundsCount; i++)
                            _reader.ReadCompressedInteger();
                        var lowerBoundsCount = _reader.ReadCompressedInteger();
                        for (int j = 0; j < lowerBoundsCount; j++)
                            _reader.ReadCompressedInteger();

                        return _module.Context.GetArrayType(elementType, rank);
                    }
                case SignatureTypeCode.ByReference:
                    return ParseType().MakeByRefType();
                case SignatureTypeCode.Pointer:
                    return _module.Context.GetPointerType(ParseType());
                case SignatureTypeCode.Pinned: // TODO: Pinned types in local signatures!
                    return ParseType();
                case SignatureTypeCode.GenericTypeParameter:
                    return _module.Context.GetSignatureVariable(_reader.ReadCompressedInteger(), false);
                case SignatureTypeCode.GenericMethodParameter:
                    return _module.Context.GetSignatureVariable(_reader.ReadCompressedInteger(), true);
                case SignatureTypeCode.GenericTypeInstance:
                    {
                        TypeDesc typeDef = ParseType();
                        MetadataType metadataTypeDef = typeDef as MetadataType;
                        if (metadataTypeDef == null)
                            throw new BadImageFormatException();

                        TypeDesc[] instance = new TypeDesc[_reader.ReadCompressedInteger()];
                        for (int i = 0; i < instance.Length; i++)
                            instance[i] = ParseType();
                        return _module.Context.GetInstantiatedType(metadataTypeDef, new Instantiation(instance));
                    }
                case SignatureTypeCode.TypedReference:
                    throw new PlatformNotSupportedException("TypedReference not supported in .NET Core");
                default:
                    throw new BadImageFormatException();
            }
        }

        private SignatureTypeCode ParseTypeCode()
        {
            for (;;)
            {
                SignatureTypeCode typeCode = _reader.ReadSignatureTypeCode();

                if (typeCode != SignatureTypeCode.RequiredModifier && typeCode != SignatureTypeCode.OptionalModifier)
                    return typeCode;

                _reader.ReadTypeHandle();

                // _hasModifiers = true;
            }
        }

        public TypeDesc ParseType()
        {
            return ParseType(ParseTypeCode());
        }

        public bool IsFieldSignature
        {
            get
            {
                BlobReader peek = _reader;
                return (peek.ReadByte() & 0xF) == 6; // IMAGE_CEE_CS_CALLCONV_FIELD - add it to SignatureCallingConvention?
            }
        }

        public MethodSignature ParseMethodSignature()
        {
            MethodSignatureFlags flags = 0;

            byte callingConvention = _reader.ReadByte();
            if ((callingConvention & (byte)SignatureAttributes.Instance) == 0)
                flags |= MethodSignatureFlags.Static;

            int arity = ((callingConvention & (byte)SignatureAttributes.Generic) != 0) ? _reader.ReadCompressedInteger() : 0;

            int count = _reader.ReadCompressedInteger();

            TypeDesc returnType = ParseType();
            TypeDesc[] parameters;

            if (count > 0)
            {
                // Get all of the parameters.
                parameters = new TypeDesc[count];
                for (int i = 0; i < count; i++)
                {
                    parameters[i] = ParseType();
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
            if ((_reader.ReadByte() & 0xF) != 6) // IMAGE_CEE_CS_CALLCONV_FIELD - add it to SignatureCallingConvention?
                throw new BadImageFormatException();

            return ParseType();
        }

        public TypeDesc[] ParseLocalsSignature()
        {
            if ((_reader.ReadByte() & 0xF) != 7) // IMAGE_CEE_CS_CALLCONV_LOCAL_SIG - add it to SignatureCallingConvention?
                throw new BadImageFormatException();

            int count = _reader.ReadCompressedInteger();

            TypeDesc[] locals;

            if (count > 0)
            {
                locals = new TypeDesc[count];
                for (int i = 0; i < count; i++)
                {
                    SignatureTypeCode typeCode = ParseTypeCode();
                    // TODO: Handle SignatureTypeCode.Pinned
                    locals[i] = ParseType(typeCode);
                }
            }
            else
            {
                locals = TypeDesc.EmptyTypes;
            }
            return locals;
        }

        public TypeDesc[] ParseMethodSpecSignature()
        {
            if ((_reader.ReadByte() & 0xF) != 0xa) // IMAGE_CEE_CS_CALLCONV_GENERICINST - add it to SignatureCallingConvention?
                throw new BadImageFormatException();

            int count = _reader.ReadCompressedInteger();

            if (count <= 0)
                throw new BadImageFormatException();

            TypeDesc[] arguments = new TypeDesc[count];
            for (int i = 0; i < count; i++)
            {
                arguments[i] = ParseType();
            }
            return arguments;
        }
    }
}
