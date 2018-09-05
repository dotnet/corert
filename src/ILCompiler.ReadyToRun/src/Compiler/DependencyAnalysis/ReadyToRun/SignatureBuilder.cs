// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
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

        public void EmitTypeSignature(TypeDesc typeDesc, SignatureContext context)
        {
            if (typeDesc.HasInstantiation && !typeDesc.IsGenericDefinition)
            {
                EmitInstantiatedTypeSignature((InstantiatedType)typeDesc, context);
                return;
            }

            switch (typeDesc.Category)
            {
                case TypeFlags.Array:
                    EmitArrayTypeSignature((ArrayType)typeDesc, context);
                    return;

                case TypeFlags.SzArray:
                    EmitSzArrayTypeSignature((ArrayType)typeDesc, context);
                    return;

                case TypeFlags.Pointer:
                    EmitPointerTypeSignature((PointerType)typeDesc, context);
                    return;

                case TypeFlags.Void:
                    EmitElementType(CorElementType.ELEMENT_TYPE_VOID);
                    return;

                case TypeFlags.Boolean:
                    EmitElementType(CorElementType.ELEMENT_TYPE_BOOLEAN);
                    return;

                case TypeFlags.Char:
                    EmitElementType(CorElementType.ELEMENT_TYPE_CHAR);
                    return;

                case TypeFlags.SByte:
                    EmitElementType(CorElementType.ELEMENT_TYPE_I1);
                    return;

                case TypeFlags.Byte:
                    EmitElementType(CorElementType.ELEMENT_TYPE_U1);
                    return;

                case TypeFlags.Int16:
                    EmitElementType(CorElementType.ELEMENT_TYPE_I2);
                    return;

                case TypeFlags.UInt16:
                    EmitElementType(CorElementType.ELEMENT_TYPE_U2);
                    return;

                case TypeFlags.Int32:
                    EmitElementType(CorElementType.ELEMENT_TYPE_I4);
                    return;

                case TypeFlags.UInt32:
                    EmitElementType(CorElementType.ELEMENT_TYPE_U4);
                    return;

                case TypeFlags.Int64:
                    EmitElementType(CorElementType.ELEMENT_TYPE_I8);
                    return;

                case TypeFlags.UInt64:
                    EmitElementType(CorElementType.ELEMENT_TYPE_U8);
                    return;

                case TypeFlags.IntPtr:
                    EmitElementType(CorElementType.ELEMENT_TYPE_I);
                    return;

                case TypeFlags.UIntPtr:
                    EmitElementType(CorElementType.ELEMENT_TYPE_U);
                    return;

                case TypeFlags.Single:
                    EmitElementType(CorElementType.ELEMENT_TYPE_R4);
                    return;

                case TypeFlags.Double:
                    EmitElementType(CorElementType.ELEMENT_TYPE_R4);
                    return;

                case TypeFlags.Interface:
                case TypeFlags.Class:
                    if (typeDesc.IsString)
                    {
                        EmitElementType(CorElementType.ELEMENT_TYPE_STRING);
                    }
                    else if (typeDesc.IsObject)
                    {
                        EmitElementType(CorElementType.ELEMENT_TYPE_OBJECT);
                    }
                    else
                    {
                        EmitElementType(CorElementType.ELEMENT_TYPE_CLASS);
                        EmitTypeToken((EcmaType)typeDesc, context);
                    }
                    return;

                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                case TypeFlags.Enum:
                    EmitElementType(CorElementType.ELEMENT_TYPE_VALUETYPE);
                    EmitTypeToken((EcmaType)typeDesc, context);
                    return;

                default:
                    throw new NotImplementedException();
            }
        }

        private void EmitTypeToken(EcmaType type, SignatureContext context)
        {
            ModuleToken token = context.GetModuleTokenForType(type);
            EmitToken(token.Token);
        }

        private void EmitInstantiatedTypeSignature(InstantiatedType type, SignatureContext context)
        {
            EmitElementType(CorElementType.ELEMENT_TYPE_GENERICINST);
            EmitTypeSignature(type.GetTypeDefinition(), context);
            EmitUInt((uint)type.Instantiation.Length);
            for (int paramIndex = 0; paramIndex < type.Instantiation.Length; paramIndex++)
            {
                EmitTypeSignature(type.Instantiation[paramIndex], context);
            }
        }

        private void EmitPointerTypeSignature(PointerType type, SignatureContext context)
        {
            EmitElementType(CorElementType.ELEMENT_TYPE_PTR);
            EmitTypeSignature(type.ParameterType, context);
        }

        private void EmitSzArrayTypeSignature(ArrayType type, SignatureContext context)
        {
            Debug.Assert(type.IsSzArray);
            EmitElementType(CorElementType.ELEMENT_TYPE_SZARRAY);
            EmitTypeSignature(type.ElementType, context);
        }

        private void EmitArrayTypeSignature(ArrayType type, SignatureContext context)
        {
            Debug.Assert(type.IsArray && !type.IsSzArray);
            EmitElementType(CorElementType.ELEMENT_TYPE_ARRAY);
            EmitTypeSignature(type.ElementType, context);
            EmitUInt((uint)type.Rank);
            if (type.Rank != 0)
            {
                EmitUInt(0); // Number of sizes
                EmitUInt(0); // Number of lower bounds
            }
        }

        public void EmitMethodSignature(MethodDesc method, TypeDesc constrainedType, bool isUnboxingStub, bool isInstantiatingStub, SignatureContext context)
        {
            ModuleToken token = context.GetModuleTokenForMethod(method);

            uint flags = 0;
            if (isUnboxingStub)
            {
                flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_UnboxingStub;
            }
            if (isInstantiatingStub)
            {
                flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_InstantiatingStub;
            }
            if (constrainedType != null)
            {
                flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_Constrained;
            }

            switch (token.TokenType)
            {
                case CorTokenType.mdtMethodDef:
                    // TODO: module override for methoddefs with external module context
                    EmitUInt(flags);
                    EmitMethodDefToken(token);
                    break;

                case CorTokenType.mdtMemberRef:
                    // TODO: module override for methodrefs with external module context
                    flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MemberRefToken;
                    EmitUInt(flags);
                    EmitMethodRefToken(token);
                    break;

                case CorTokenType.mdtMethodSpec:
                    EmitMethodSpecificationSignature(method, token, flags, context);
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (constrainedType != null)
            {
                EmitTypeSignature(constrainedType, context);
            }
        }

        public void EmitMethodDefToken(ModuleToken methodDefToken)
        {
            Debug.Assert(methodDefToken.TokenType == CorTokenType.mdtMethodDef);
            EmitUInt(methodDefToken.TokenRid);
        }

        public void EmitMethodRefToken(ModuleToken memberRefToken)
        {
            Debug.Assert(memberRefToken.TokenType == CorTokenType.mdtMemberRef);
            EmitUInt(RidFromToken(memberRefToken.Token));
        }

        private void EmitMethodSpecificationSignature(MethodDesc method, ModuleToken token, uint flags, SignatureContext context)
        {
            switch (token.TokenType)
            {
                case CorTokenType.mdtMethodSpec:
                    {
                        MethodSpecification methodSpec = token.MetadataReader.GetMethodSpecification((MethodSpecificationHandle)MetadataTokens.Handle((int)token.Token));

                        flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MethodInstantiation;
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
                                flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MemberRefToken;
                                genericMethodToken = (mdToken)MetadataTokens.GetToken(methodSpec.Method);
                                break;

                            default:
                                throw new NotImplementedException();
                        }

                        EmitUInt(flags);
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

        public void EmitFieldSignature(FieldDesc field, SignatureContext context)
        {
            ModuleToken fieldRefToken = context.GetModuleTokenForField(field);
            switch (fieldRefToken.TokenType)
            {
                case CorTokenType.mdtMemberRef:
                    EmitUInt((uint)ReadyToRunFieldSigFlags.READYTORUN_FIELD_SIG_MemberRefToken);
                    EmitTokenRid(fieldRefToken.Token);
                    break;

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

        public void EmitReloc(ISymbolNode symbol, RelocType relocType, int delta = 0)
        {
            _builder.EmitReloc(symbol, relocType, delta);
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
        private readonly ModuleTokenResolver _resolver;

        private readonly EcmaModule _contextModule;

        public SignatureContext(ModuleTokenResolver resolver, EcmaModule contextModule)
        {
            _resolver = resolver;
            _contextModule = contextModule;
        }

        public MetadataReader MetadataReader => _contextModule.MetadataReader;

        public ModuleToken GetModuleTokenForType(EcmaType type)
        {
            return _resolver.GetModuleTokenForType(type);
        }

        public ModuleToken GetModuleTokenForMethod(MethodDesc method)
        {
            return _resolver.GetModuleTokenForMethod(method);
        }

        public ModuleToken GetModuleTokenForField(FieldDesc field)
        {
            return _resolver.GetModuleTokenForField(field);
        }

        public byte[] GetArrayType(byte[] elementType, ArrayShape shape)
        {
            ArraySignatureBuilder builder = new ArraySignatureBuilder();
            builder.EmitByte((byte)CorElementType.ELEMENT_TYPE_ARRAY);
            builder.EmitBytes(elementType);
            builder.EmitUInt((uint)shape.Rank);
            if (shape.Rank != 0)
            {
                builder.EmitUInt((uint)shape.Sizes.Length);
                foreach (int size in shape.Sizes)
                {
                    builder.EmitUInt((uint)size);
                }
                builder.EmitUInt((uint)shape.LowerBounds.Length);
                foreach (int lowerBound in shape.LowerBounds)
                {
                    builder.EmitInt(lowerBound);
                }
            }
            return builder.ToArray();
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
            ArraySignatureBuilder builder = new ArraySignatureBuilder();
            builder.EmitElementType(CorElementType.ELEMENT_TYPE_MVAR);
            builder.EmitUInt((uint)index);
            return builder.ToArray();
        }

        public byte[] GetGenericTypeParameter(SignatureContext genericContext, int index)
        {
            ArraySignatureBuilder builder = new ArraySignatureBuilder();
            builder.EmitElementType(CorElementType.ELEMENT_TYPE_VAR);
            builder.EmitUInt((uint)index);
            return builder.ToArray();
        }

        public byte[] GetModifiedType(byte[] modifier, byte[] unmodifiedType, bool isRequired)
        {
            throw new NotImplementedException();
        }

        public byte[] GetPinnedType(byte[] elementType)
        {
            byte[] output = new byte[elementType.Length + 1];
            output[0] = (byte)CorElementType.ELEMENT_TYPE_PINNED;
            Array.Copy(elementType, 0, output, 1, elementType.Length);
            return output;
        }

        public byte[] GetPointerType(byte[] elementType)
        {
            byte[] output = new byte[elementType.Length + 1];
            output[0] = (byte)CorElementType.ELEMENT_TYPE_PTR;
            Array.Copy(elementType, 0, output, 1, elementType.Length);
            return output;
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
            byte[] outputSignature = new byte[elementType.Length + 1];
            outputSignature[0] = (byte)CorElementType.ELEMENT_TYPE_SZARRAY;
            Array.Copy(elementType, 0, outputSignature, 1, elementType.Length);
            return outputSignature;
        }

        public byte[] GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            // If the two readers mismatch, we must do something mode complicated and I don't yet know what
            Debug.Assert(_contextModule.MetadataReader == reader);
            TypeDesc type = (TypeDesc)_contextModule.GetObject(handle);
            ArraySignatureBuilder builder = new ArraySignatureBuilder();
            builder.EmitTypeSignature(type, this);
            return builder.ToArray();
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
