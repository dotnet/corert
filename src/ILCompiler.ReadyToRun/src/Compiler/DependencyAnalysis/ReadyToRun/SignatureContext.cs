// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class SignatureContext : ISignatureTypeProvider<byte[], SignatureContext>
    {
        private readonly ModuleTokenResolver _resolver;

        private readonly CompilerTypeSystemContext _typeSystemContext;

        public SignatureContext(ModuleTokenResolver resolver, CompilerTypeSystemContext typeSystemContext)
        {
            _resolver = resolver;
            _typeSystemContext = typeSystemContext;
        }

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
            EcmaModule module = _typeSystemContext.GetModuleFromMetadataReader(reader);
            TypeDesc type = (TypeDesc)module.GetObject(handle);
            ArraySignatureBuilder builder = new ArraySignatureBuilder();
            builder.EmitTypeSignature(type, this);
            return builder.ToArray();
        }

        public byte[] GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            ArraySignatureBuilder refBuilder = new ArraySignatureBuilder();
            EcmaModule module = _typeSystemContext.GetModuleFromMetadataReader(reader);
            TypeDesc type = (TypeDesc)module.GetObject(handle);

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
