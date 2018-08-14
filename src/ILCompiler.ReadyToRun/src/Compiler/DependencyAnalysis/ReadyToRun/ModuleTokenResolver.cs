// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// This class is used to back-resolve typesystem elements from
    /// external version bubbles to references relative to the current
    /// versioning bubble.
    /// </summary>
    public class ModuleTokenResolver
    {
        /// <summary>
        /// Reverse lookup table mapping external types to reference tokens in the input modules. The table
        /// gets lazily initialized as various tokens are resolved in CorInfoImpl.
        /// </summary>
        private readonly Dictionary<EcmaType, ModuleToken> _typeToRefTokens = new Dictionary<EcmaType, ModuleToken>();

        private readonly CompilationModuleGroup _compilationModuleGroup;

        public ModuleTokenResolver(CompilationModuleGroup compilationModuleGroup)
        {
            _compilationModuleGroup = compilationModuleGroup;
        }

        public ModuleToken GetModuleTokenForType(EcmaType type)
        {
            if (_compilationModuleGroup.ContainsType(type))
            {
                return new ModuleToken(type.EcmaModule, (mdToken)MetadataTokens.GetToken(type.Handle));
            }

            ModuleToken token;
            if (_typeToRefTokens.TryGetValue(type, out token))
            {
                return token;
            }

            // Reverse lookup failed
            throw new NotImplementedException(type.ToString());
        }

        public void AddModuleTokenForMethod(MethodDesc method, ModuleToken token)
        {
            if (_compilationModuleGroup.ContainsMethodBody(method, unboxingStub: false))
            {
                // We don't need to store handles within the current compilation group
                // as we can read them directly from the ECMA objects.
                return;
            }

            switch (token.TokenType)
            {
                case CorTokenType.mdtMemberRef:
                    AddModuleTokenForMemberReference(method.OwningType, token);
                    break;

                default:
                    throw new NotImplementedException(token.TokenType.ToString());
            }
        }

        private void AddModuleTokenForMemberReference(TypeDesc owningType, ModuleToken token)
        {
            MemberReference memberRef = token.MetadataReader.GetMemberReference((MemberReferenceHandle)token.Handle);
            EntityHandle owningTypeHandle = memberRef.Parent;
            AddModuleTokenForType(owningType, new ModuleToken(token.Module, owningTypeHandle));
        }

        public void AddModuleTokenForField(FieldDesc field, ModuleToken token)
        {
            if (_compilationModuleGroup.ContainsType(field.OwningType))
            {
                // We don't need to store handles within the current compilation group
                // as we can read them directly from the ECMA objects.
                return;
            }

            switch (token.TokenType)
            {
                case CorTokenType.mdtMemberRef:
                    AddModuleTokenForMemberReference(field.OwningType, token);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public void AddModuleTokenForType(TypeDesc type, ModuleToken token)
        {
            if (_compilationModuleGroup.ContainsType(type))
            {
                // We don't need to store handles within the current compilation group
                // as we can read them directly from the ECMA objects.
                return;
            }

            if (type is EcmaType ecmaType)
            {
                _typeToRefTokens[ecmaType] = token;
            }
            else if (type is InstantiatedType instantiatedType)
            {
                switch (token.TokenType)
                {
                    case CorTokenType.mdtTypeSpec:
                        {
                            TypeSpecification typeSpec = token.MetadataReader.GetTypeSpecification((TypeSpecificationHandle)token.Handle);
                            EntityHandle genericType = typeSpec.DecodeSignature(new GenericTypeProvider(this), this);
                            if (!genericType.IsNil && instantiatedType.GetTypeDefinition() is EcmaType ecmaTypeDef)
                            {
                                _typeToRefTokens[ecmaTypeDef] = new ModuleToken(token.Module, genericType);
                            }
                        }
                        break;
                }
            }
            else
            {
                throw new NotImplementedException(type.ToString());
            }
        }

        private class GenericTypeProvider : ISignatureTypeProvider<EntityHandle, ModuleTokenResolver>
        {
            ModuleTokenResolver _resolver;

            public GenericTypeProvider(ModuleTokenResolver resolver)
            {
                _resolver = resolver;
            }

            public EntityHandle GetArrayType(EntityHandle elementType, ArrayShape shape)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetByReferenceType(EntityHandle elementType)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetFunctionPointerType(MethodSignature<EntityHandle> signature)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetGenericInstantiation(EntityHandle genericType, ImmutableArray<EntityHandle> typeArguments)
            {
                return genericType;
            }

            public EntityHandle GetGenericMethodParameter(ReadyToRunCodegenNodeFactory genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetGenericMethodParameter(ModuleTokenResolver genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetGenericTypeParameter(ReadyToRunCodegenNodeFactory genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetGenericTypeParameter(ModuleTokenResolver genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetModifiedType(EntityHandle modifier, EntityHandle unmodifiedType, bool isRequired)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetPinnedType(EntityHandle elementType)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetPointerType(EntityHandle elementType)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return default(EntityHandle);
            }

            public EntityHandle GetSZArrayType(EntityHandle elementType)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                return handle;
            }

            public EntityHandle GetTypeFromSpecification(MetadataReader reader, ReadyToRunCodegenNodeFactory genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }

            public EntityHandle GetTypeFromSpecification(MetadataReader reader, ModuleTokenResolver genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }
        }
    }
}

