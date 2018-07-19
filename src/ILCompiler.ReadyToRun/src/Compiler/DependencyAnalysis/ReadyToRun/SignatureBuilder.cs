// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.Runtime;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.JitInterface;
using System.Collections.Immutable;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public abstract class SignatureBuilder
    {
        public abstract void EmitByte(byte data);

        public void EmitBytes(byte[] data)
        {
            foreach (byte b in data)
            {
                EmitByte(b);
            }
        }

        public void EmitUInt(uint data)
        {
            if (data <= 0x7F)
            {
                EmitByte((byte)data);
                return;
            }

            if (data <= 0x3FFF)
            {
                EmitByte((byte)((data >> 8) | 0x80));
                EmitByte((byte)(data & 0xFF));
                return;
            }

            if (data <= 0x1FFFFFFF)
            {
                EmitByte((byte)((data >> 24) | 0xC0));
                EmitByte((byte)((data >> 16) & 0xff));
                EmitByte((byte)((data >> 8) & 0xff));
                EmitByte((byte)(data & 0xff));
                return;
            }

            throw new NotImplementedException();
        }

        public static uint RidFromToken(mdToken token)
        {
            return unchecked((uint)token) & 0x00FFFFFFu;
        }

        public static CorTokenType TypeFromToken(int token)
        {
            return (CorTokenType)(unchecked((uint)token) & 0xFF000000u);
        }

        public static CorTokenType TypeFromToken(mdToken token)
        {
            return TypeFromToken((int)token);
        }

        public void EmitTokenRid(mdToken token)
        {
            EmitUInt((uint)RidFromToken(token));
        }

        // compress a token
        // The least significant bit of the first compress byte will indicate the token type.
        //
        public void EmitToken(mdToken token)
        {
            uint rid = RidFromToken(token);
            CorTokenType type = (CorTokenType)TypeFromToken(token);

            if (rid > 0x3FFFFFF)
            {
                // token is too big to be compressed
                throw new NotImplementedException();
            }

            rid = (rid << 2);

            // TypeDef is encoded with low bits 00
            // TypeRef is encoded with low bits 01
            // TypeSpec is encoded with low bits 10
            // BaseType is encoded with low bit 11
            switch (type)
            {
                case CorTokenType.mdtTypeDef:
                    break;

                case CorTokenType.mdtTypeRef:
                    // make the last two bits 01
                    rid |= 0x1;
                    break;

                case CorTokenType.mdtTypeSpec:
                    // make last two bits 0
                    rid |= 0x2;
                    break;

                case CorTokenType.mdtBaseType:
                    rid |= 0x3;
                    break;

                default:
                    throw new NotImplementedException();
            }

            EmitUInt(rid);
        }

        private static class SignMask
        {
            public const uint ONEBYTE = 0xffffffc0; // Mask the same size as the missing bits.
            public const uint TWOBYTE = 0xffffe000; // Mask the same size as the missing bits.
            public const uint FOURBYTE = 0xf0000000; // Mask the same size as the missing bits.
        }

        /// <summary>
        /// Compress a signed integer. The least significant bit of the first compressed byte will be the sign bit.
        /// </summary>
        public void EmitInt(int data)
        {
            uint isSigned = (data < 0 ? 1u : 0u);
            uint udata = unchecked((uint)data);

            // Note that we cannot use CompressData to pack the data value, because of negative values 
            // like: 0xffffe000 (-8192) which has to be encoded as 1 in 2 bytes, i.e. 0x81 0x00
            // However CompressData would store value 1 as 1 byte: 0x01
            if ((udata & SignMask.ONEBYTE) == 0 || (udata & SignMask.ONEBYTE) == SignMask.ONEBYTE)
            {
                udata = ((udata & ~SignMask.ONEBYTE) << 1 | isSigned);
                Debug.Assert(udata <= 0x7f);
                EmitByte((byte)udata);
                return;
            }

            if ((udata & SignMask.TWOBYTE) == 0 || (udata & SignMask.TWOBYTE) == SignMask.TWOBYTE)
            {
                udata = ((udata & ~SignMask.TWOBYTE) << 1 | isSigned);
                Debug.Assert(udata <= 0x3fff);
                EmitByte((byte)((udata >> 8) | 0x80));
                EmitByte((byte)(udata & 0xff));
                return;
            }

            if ((udata & SignMask.FOURBYTE) == 0 || (udata & SignMask.FOURBYTE) == SignMask.FOURBYTE)
            {
                udata = ((udata & ~SignMask.FOURBYTE) << 1 | isSigned);
                Debug.Assert(udata <= 0x1FFFFFFF);
                EmitByte((byte)((udata >> 24) | 0xC0));
                EmitByte((byte)((udata >> 16) & 0xff));
                EmitByte((byte)((udata >> 8) & 0xff));
                EmitByte((byte)(udata & 0xff));
                return;
            }

            // Out of compressable range
            throw new NotImplementedException();
        }

        /// <summary>
        /// Compress a CorElementType into a single byte.
        /// </summary>
        /// <param name="elementType">COR element type to compress</param>
        internal void EmitElementType(CorElementType elementType)
        {
            EmitByte((byte)elementType);
        }

        public void EmitTypeSignature(TypeDesc typeDesc, mdToken token, SignatureContext context)
        {
            if (typeDesc.HasInstantiation)
            {
                EmitTypeSpecification(token, context);
                return;
            }

            CorElementType elementType;
            if (typeDesc.IsPrimitive)
            {
                switch (typeDesc.Category)
                {
                    case TypeFlags.Void:
                        elementType = CorElementType.ELEMENT_TYPE_VOID;
                        break;

                    case TypeFlags.Boolean:
                        elementType = CorElementType.ELEMENT_TYPE_BOOLEAN;
                        break;

                    case TypeFlags.Char:
                        elementType = CorElementType.ELEMENT_TYPE_CHAR;
                        break;

                    case TypeFlags.SByte:
                        elementType = CorElementType.ELEMENT_TYPE_I1;
                        break;

                    case TypeFlags.Byte:
                        elementType = CorElementType.ELEMENT_TYPE_U1;
                        break;

                    case TypeFlags.Int16:
                        elementType = CorElementType.ELEMENT_TYPE_I2;
                        break;

                    case TypeFlags.UInt16:
                        elementType = CorElementType.ELEMENT_TYPE_U2;
                        break;

                    case TypeFlags.Int32:
                        elementType = CorElementType.ELEMENT_TYPE_I4;
                        break;

                    case TypeFlags.UInt32:
                        elementType = CorElementType.ELEMENT_TYPE_U4;
                        break;

                    case TypeFlags.Int64:
                        elementType = CorElementType.ELEMENT_TYPE_I8;
                        break;

                    case TypeFlags.UInt64:
                        elementType = CorElementType.ELEMENT_TYPE_U8;
                        break;

                    case TypeFlags.IntPtr:
                        elementType = CorElementType.ELEMENT_TYPE_I;
                        break;

                    case TypeFlags.UIntPtr:
                        elementType = CorElementType.ELEMENT_TYPE_U;
                        break;

                    case TypeFlags.Single:
                        elementType = CorElementType.ELEMENT_TYPE_R4;
                        break;

                    case TypeFlags.Double:
                        elementType = CorElementType.ELEMENT_TYPE_R4;
                        break;

                    default:
                        throw new NotImplementedException();
                }

                EmitByte((byte)elementType);
                return;
            }

            if (typeDesc is ArrayType arrayType)
            {
                EmitByte((byte)CorElementType.ELEMENT_TYPE_SZARRAY);
                EmitTypeSignature(arrayType.ElementType, token, context);
                return;
            }

            if (typeDesc.IsValueType)
            {
                elementType = CorElementType.ELEMENT_TYPE_VALUETYPE;
            }
            else
            {
                elementType = CorElementType.ELEMENT_TYPE_CLASS;
            }

            EmitElementType(elementType);

            switch (TypeFromToken(token))
            {
                case CorTokenType.mdtTypeDef:
                case CorTokenType.mdtTypeRef:
                case CorTokenType.mdtTypeSpec:
                    EmitToken(token);
                    return;

                default:
                    throw new NotImplementedException();
            }
        }

        private void EmitTypeSpecification(mdToken token, SignatureContext context)
        {
            Debug.Assert(TypeFromToken(token) == CorTokenType.mdtTypeSpec);
            TypeSpecification typeSpec = context.MetadataReader.GetTypeSpecification((TypeSpecificationHandle)MetadataTokens.Handle((int)token));
            BlobReader signatureReader = context.MetadataReader.GetBlobReader(typeSpec.Signature);
            SignatureDecoder<byte[], SignatureContext> decoder = new SignatureDecoder<byte[], SignatureContext>(context, context.MetadataReader, context);
            EmitBytes(decoder.DecodeType(ref signatureReader, allowTypeSpecifications: true));
        }

        public void EmitMethodSignature(MethodDesc method, mdToken token, SignatureContext context)
        {
            switch (TypeFromToken(token))
            {
                case CorTokenType.mdtMethodDef:
                    EmitUInt((uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_None);
                    EmitMethodDefToken(token);
                    break;

                case CorTokenType.mdtMemberRef:
                    EmitUInt((uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MemberRefToken);
                    EmitMethodRefToken(token);
                    break;

                case CorTokenType.mdtMethodSpec:
                    EmitMethodSpecificationSignature(method, token, context);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public void EmitMethodDefToken(mdToken methodDefToken)
        {
            Debug.Assert(TypeFromToken(methodDefToken) == CorTokenType.mdtMethodDef);
            EmitUInt(RidFromToken(methodDefToken));
        }

        public void EmitMethodRefToken(mdToken memberRefToken)
        {
            Debug.Assert(TypeFromToken(memberRefToken) == CorTokenType.mdtMemberRef);
            EmitUInt(RidFromToken(memberRefToken));
        }

        private void EmitMethodSpecificationSignature(MethodDesc method, mdToken token, SignatureContext context)
        {
            switch (TypeFromToken(token))
            {
                case CorTokenType.mdtMethodSpec:
                    {
                        MethodSpecification methodSpec = context.MetadataReader.GetMethodSpecification((MethodSpecificationHandle)MetadataTokens.Handle((int)token));

                        ReadyToRunMethodSigFlags flags = ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MethodInstantiation;
                        mdToken genericMethodToken;

                        switch (methodSpec.Method.Kind)
                        {
                            case HandleKind.MethodDefinition:
                                {
                                    genericMethodToken = (mdToken)MetadataTokens.GetToken(methodSpec.Method);
                                    MethodDefinition methodDef = context.MetadataReader.GetMethodDefinition((MethodDefinitionHandle)methodSpec.Method);
                                    TypeDefinitionHandle typeHandle = methodDef.GetDeclaringType();
                                }
                                break;

                            case HandleKind.MemberReference:
                                flags |= ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MemberRefToken;
                                genericMethodToken = (mdToken)MetadataTokens.GetToken(methodSpec.Method);
                                break;

                            default:
                                throw new NotImplementedException();
                        }

                        EmitUInt((uint)flags);
                        EmitTokenRid(genericMethodToken);
                        ImmutableArray<byte[]> methodTypeSignatures = methodSpec.DecodeSignature<byte[], SignatureContext>(context, context);
                        EmitUInt((uint)methodTypeSignatures.Length);
                        foreach (byte[] methodTypeSignature in methodTypeSignatures)
                        {
                            EmitBytes(methodTypeSignature);
                        }

                        break;
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public class ObjectDataSignatureBuilder : SignatureBuilder
    {
        private ObjectDataBuilder _builder;

        public ObjectDataSignatureBuilder()
        {
            _builder = new ObjectDataBuilder();
        }

        public void AddSymbol(ISymbolDefinitionNode symbol)
        {
            _builder.AddSymbol(symbol);
        }

        public override void EmitByte(byte data)
        {
            _builder.EmitByte(data);
        }

        public ObjectNode.ObjectData ToObjectData()
        {
            return _builder.ToObjectData();
        }
    }

    internal class ArraySignatureBuilder : SignatureBuilder
    {
        private ArrayBuilder<byte> _builder;

        public ArraySignatureBuilder()
        {
            _builder = new ArrayBuilder<byte>();
        }

        public override void EmitByte(byte data)
        {
            _builder.Add(data);
        }

        public byte[] ToArray()
        {
            return _builder.ToArray();
        }
    }

    public class SignatureContext : ISignatureTypeProvider<byte[], SignatureContext>
    {
        private readonly ReadyToRunCodegenNodeFactory _nodeFactory;

        private readonly EcmaModule _contextModule;

        public SignatureContext(ReadyToRunCodegenNodeFactory nodeFactory, EcmaModule contextModule)
        {
            _nodeFactory = nodeFactory;
            _contextModule = contextModule;
        }

        public MetadataReader MetadataReader => _nodeFactory.PEReader.GetMetadataReader();

        public byte[] GetArrayType(byte[] elementType, ArrayShape shape)
        {
            throw new NotImplementedException();
        }

        public byte[] GetByReferenceType(byte[] elementType)
        {
            throw new NotImplementedException();
        }

        public byte[] GetFunctionPointerType(MethodSignature<byte[]> signature)
        {
            throw new NotImplementedException();
        }

        public byte[] GetGenericInstantiation(byte[] genericType, ImmutableArray<byte[]> typeArguments)
        {
            ArraySignatureBuilder builder = new ArraySignatureBuilder();
            builder.EmitElementType(CorElementType.ELEMENT_TYPE_GENERICINST);
            builder.EmitBytes(genericType);
            builder.EmitUInt((uint)typeArguments.Length);
            foreach (byte[] typeArgSignature in typeArguments)
            {
                builder.EmitBytes(typeArgSignature);
            }
            return builder.ToArray();
        }

        public byte[] GetGenericMethodParameter(SignatureContext genericContext, int index)
        {
            throw new NotImplementedException();
        }

        public byte[] GetGenericTypeParameter(SignatureContext genericContext, int index)
        {
            throw new NotImplementedException();
        }

        public byte[] GetModifiedType(byte[] modifier, byte[] unmodifiedType, bool isRequired)
        {
            throw new NotImplementedException();
        }

        public byte[] GetPinnedType(byte[] elementType)
        {
            throw new NotImplementedException();
        }

        public byte[] GetPointerType(byte[] elementType)
        {
            throw new NotImplementedException();
        }

        public byte[] GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            CorElementType elementType;
            switch (typeCode)
            {
                case PrimitiveTypeCode.Void:
                    elementType = CorElementType.ELEMENT_TYPE_VOID;
                    break;

                case PrimitiveTypeCode.Boolean:
                    elementType = CorElementType.ELEMENT_TYPE_BOOLEAN;
                    break;

                case PrimitiveTypeCode.Char:
                    elementType = CorElementType.ELEMENT_TYPE_CHAR;
                    break;

                case PrimitiveTypeCode.SByte:
                    elementType = CorElementType.ELEMENT_TYPE_I1;
                    break;

                case PrimitiveTypeCode.Byte:
                    elementType = CorElementType.ELEMENT_TYPE_U1;
                    break;

                case PrimitiveTypeCode.Int16:
                    elementType = CorElementType.ELEMENT_TYPE_I2;
                    break;

                case PrimitiveTypeCode.UInt16:
                    elementType = CorElementType.ELEMENT_TYPE_U2;
                    break;

                case PrimitiveTypeCode.Int32:
                    elementType = CorElementType.ELEMENT_TYPE_I4;
                    break;

                case PrimitiveTypeCode.UInt32:
                    elementType = CorElementType.ELEMENT_TYPE_U4;
                    break;

                case PrimitiveTypeCode.Int64:
                    elementType = CorElementType.ELEMENT_TYPE_I8;
                    break;

                case PrimitiveTypeCode.UInt64:
                    elementType = CorElementType.ELEMENT_TYPE_U8;
                    break;

                case PrimitiveTypeCode.Single:
                    elementType = CorElementType.ELEMENT_TYPE_R4;
                    break;

                case PrimitiveTypeCode.Double:
                    elementType = CorElementType.ELEMENT_TYPE_R8;
                    break;

                case PrimitiveTypeCode.String:
                    elementType = CorElementType.ELEMENT_TYPE_STRING;
                    break;

                case PrimitiveTypeCode.TypedReference:
                    elementType = CorElementType.ELEMENT_TYPE_TYPEDBYREF;
                    break;

                case PrimitiveTypeCode.IntPtr:
                    elementType = CorElementType.ELEMENT_TYPE_I;
                    break;

                case PrimitiveTypeCode.UIntPtr:
                    elementType = CorElementType.ELEMENT_TYPE_U;
                    break;

                case PrimitiveTypeCode.Object:
                    elementType = CorElementType.ELEMENT_TYPE_OBJECT;
                    break;

                default:
                    throw new NotImplementedException();
            }

            return new byte[] { (byte)elementType };
        }

        public byte[] GetSZArrayType(byte[] elementType)
        {
            throw new NotImplementedException();
        }

        public byte[] GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            throw new NotImplementedException();
        }

        public byte[] GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            ArraySignatureBuilder refBuilder = new ArraySignatureBuilder();
            TypeDesc type = (TypeDesc)_contextModule.GetObject(handle);

            refBuilder.EmitElementType(type.IsValueType ? CorElementType.ELEMENT_TYPE_VALUETYPE : CorElementType.ELEMENT_TYPE_CLASS);
            refBuilder.EmitToken((mdToken)MetadataTokens.GetToken(handle));
            return refBuilder.ToArray();
        }

        public byte[] GetTypeFromSpecification(MetadataReader reader, SignatureContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            throw new NotImplementedException();
        }
    }
}
