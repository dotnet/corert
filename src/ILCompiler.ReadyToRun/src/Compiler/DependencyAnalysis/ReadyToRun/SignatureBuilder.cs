﻿// The .NET Foundation licenses this file to you under the MIT license.
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

        private void EmitElementTypeWithModuleOverride(CorElementType elementType, EcmaModule targetModule, SignatureContext context)
        {
            int moduleImportIndex = context.GetModuleIndex(targetModule);
            if (moduleImportIndex < 0)
            {
                EmitElementType(elementType);
            }
            else
            {
                EmitElementType(elementType | CorElementType.ELEMENT_TYPE_MODULE_OVERRIDE);
                EmitUInt((uint)moduleImportIndex);
            }
        }

        public void EmitTypeSignature(TypeDesc typeDesc, SignatureContext context)
        {
            if (typeDesc is RuntimeDeterminedType runtimeDeterminedType)
            {
                switch (runtimeDeterminedType.RuntimeDeterminedDetailsType.Kind)
                {
                    case GenericParameterKind.Type:
                        EmitElementType(CorElementType.ELEMENT_TYPE_VAR);
                        break;

                    case GenericParameterKind.Method:
                        EmitElementType(CorElementType.ELEMENT_TYPE_MVAR);
                        break;

                    default:
                        throw new NotImplementedException();
                }
                EmitUInt((uint)runtimeDeterminedType.RuntimeDeterminedDetailsType.Index);
                return;
            }

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
                    EmitElementType(CorElementType.ELEMENT_TYPE_R8);
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
                    else if (typeDesc.IsCanonicalDefinitionType(CanonicalFormKind.Specific))
                    {
                        EmitElementType(CorElementType.ELEMENT_TYPE_CANON_ZAPSIG);
                    }
                    else
                    {
                        EcmaType ecmaType = (EcmaType)typeDesc;
                        ModuleToken token = context.GetModuleTokenForType(ecmaType);
                        EmitElementTypeWithModuleOverride(CorElementType.ELEMENT_TYPE_CLASS, token.Module, context);
                        EmitToken(token.Token);
                    }
                    return;

                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                case TypeFlags.Enum:
                    {
                        EcmaType ecmaType = (EcmaType)typeDesc;
                        ModuleToken token = context.GetModuleTokenForType(ecmaType);
                        EmitElementTypeWithModuleOverride(CorElementType.ELEMENT_TYPE_VALUETYPE, token.Module, context);
                        EmitToken(token.Token);
                    }
                    return;

                default:
                    throw new NotImplementedException();
            }
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

        public void EmitMethodSignature(
            MethodDesc method, 
            TypeDesc constrainedType,
            ModuleToken methodToken,
            bool enforceDefEncoding,
            SignatureContext context,
            bool isUnboxingStub,
            bool isInstantiatingStub)
        {
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

            if (methodToken.IsNull)
            {
                methodToken = context.GetModuleTokenForMethod(method.GetMethodDefinition(), throwIfNotFound: false);
            }
            if (methodToken.IsNull)
            {
                methodToken = context.GetModuleTokenForMethod(method.GetTypicalMethodDefinition(), throwIfNotFound: true);
            }

            if (method.HasInstantiation || method.OwningType.HasInstantiation)
            {
                EmitMethodSpecificationSignature(method, methodToken, flags, enforceDefEncoding, context);
            }
            else
            {
                switch (methodToken.TokenType)
                {
                    case CorTokenType.mdtMethodDef:
                        // TODO: module override for methoddefs with external module context
                        EmitUInt(flags);
                        EmitMethodDefToken(methodToken);
                        break;

                    case CorTokenType.mdtMemberRef:
                        // TODO: module override for methodrefs with external module context
                        flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MemberRefToken;
                        EmitUInt(flags);
                        EmitMethodRefToken(methodToken);
                        break;

                    default:
                        throw new NotImplementedException();
                }
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

        private void EmitMethodSpecificationSignature(MethodDesc method, ModuleToken methodToken, 
            uint flags, bool enforceDefEncoding, SignatureContext context)
        {
            if (method.HasInstantiation)
            {
                flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MethodInstantiation;

                if (methodToken.IsNull && !enforceDefEncoding)
                {
                    methodToken = context.GetModuleTokenForMethod(method.GetMethodDefinition(), throwIfNotFound: false);
                }
                if (methodToken.IsNull)
                {
                    flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_OwnerType;
                    methodToken = context.GetModuleTokenForMethod(method.GetTypicalMethodDefinition());
                }

                if (!methodToken.IsNull)
                {
                    switch (methodToken.TokenType)
                    {
                        case CorTokenType.mdtMethodSpec:
                            {
                                MethodSpecification methodSpecification = methodToken.MetadataReader.GetMethodSpecification((MethodSpecificationHandle)methodToken.Handle);
                                methodToken = new ModuleToken(methodToken.Module, methodSpecification.Method);
                            }
                            break;
                        case CorTokenType.mdtMethodDef:
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            if (method.OwningType.HasInstantiation)
            {
                // resolveToken currently resolves the token in the context of a given scope;
                // in such case, we receive a method on instantiated type along with the
                // generic definition token.
                flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_OwnerType;
            }

            switch (methodToken.TokenType)
            {
                case CorTokenType.mdtMethodDef:
                    break;

                case CorTokenType.mdtMemberRef:
                    flags |= (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MemberRefToken;
                    break;

                default:
                    throw new NotImplementedException();
            }

            EmitUInt(flags);
            if ((flags & (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_OwnerType) != 0)
            {
                EmitTypeSignature(method.OwningType, context);
            }
            EmitTokenRid(methodToken.Token);
            if ((flags & (uint)ReadyToRunMethodSigFlags.READYTORUN_METHOD_SIG_MethodInstantiation) != 0)
            {
                Instantiation instantiation = method.Instantiation;
                EmitUInt((uint)instantiation.Length);
                for (int typeParamIndex = 0; typeParamIndex < instantiation.Length; typeParamIndex++)
                {
                    EmitTypeSignature(instantiation[typeParamIndex], context);
                }
            }
        }

        public void EmitFieldSignature(FieldDesc field, SignatureContext context)
        {
            ModuleToken fieldToken = context.GetModuleTokenForField(field);
            switch (fieldToken.TokenType)
            {
                case CorTokenType.mdtMemberRef:
                    EmitUInt((uint)ReadyToRunFieldSigFlags.READYTORUN_FIELD_SIG_MemberRefToken);
                    EmitTokenRid(fieldToken.Token);
                    break;

                case CorTokenType.mdtFieldDef:
                    EmitUInt((uint)0);
                    EmitTokenRid(fieldToken.Token);
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
}
