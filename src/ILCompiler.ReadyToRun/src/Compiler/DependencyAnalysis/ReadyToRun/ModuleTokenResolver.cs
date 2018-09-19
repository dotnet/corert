﻿// Licensed to the .NET Foundation under one or more agreements.
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

        private readonly Dictionary<MethodDesc, ModuleToken> _methodToRefTokens = new Dictionary<MethodDesc, ModuleToken>();

        private readonly Dictionary<FieldDesc, ModuleToken> _fieldToRefTokens = new Dictionary<FieldDesc, ModuleToken>();

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

        public ModuleToken GetModuleTokenForMethod(MethodDesc method)
        {
            if (_compilationModuleGroup.ContainsMethodBody(method, unboxingStub: false) &&
                method is EcmaMethod ecmaMethod)
            {
                return new ModuleToken(ecmaMethod.Module, ecmaMethod.Handle);
            }

            if (_methodToRefTokens.TryGetValue(method, out ModuleToken token))
            {
                return token;
            }

            // Reverse lookup failed
            throw new NotImplementedException(method.ToString());
        }

        public ModuleToken GetModuleTokenForField(FieldDesc field)
        {
            if (_compilationModuleGroup.ContainsType(field.OwningType) && field is EcmaField ecmaField)
            {
                return new ModuleToken(ecmaField.Module, ecmaField.Handle);
            }

            throw new NotImplementedException();
        }

        public void AddModuleTokenForMethod(MethodDesc method, ModuleToken token)
        {
            if (_compilationModuleGroup.ContainsMethodBody(method, unboxingStub: false) && method is EcmaMethod)
            {
                // We don't need to store handles within the current compilation group
                // as we can read them directly from the ECMA objects.
                return;
            }

            _methodToRefTokens[method] = token;

            switch (token.TokenType)
            {
                case CorTokenType.mdtMethodSpec:
                    {
                        MethodSpecification methodSpec = token.MetadataReader.GetMethodSpecification((MethodSpecificationHandle)token.Handle);
                        AddModuleTokenForMethod((MethodDesc)token.Module.GetObject(methodSpec.Method), new ModuleToken(token.Module, methodSpec.Method));
                    }
                    break;

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
            InstantiatedType instantiatedType = type as InstantiatedType;
            if (instantiatedType != null)
            {
                // Collect type tokens for generic arguments
                switch (token.TokenType)
                {
                    case CorTokenType.mdtTypeSpec:
                        {
                            TypeSpecification typeSpec = token.MetadataReader.GetTypeSpecification((TypeSpecificationHandle)token.Handle);
                            typeSpec.DecodeSignature(new TokenResolverProvider(this, token.Module), this);
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            if (_compilationModuleGroup.ContainsType(type))
            {
                // We don't need to store handles within the current compilation group
                // as we can read them directly from the ECMA objects.
                return;
            }

            if (type is EcmaType ecmaType)
            {
                // Don't store typespec tokens where a generic parameter resolves to the type in question
                if (token.TokenType == CorTokenType.mdtTypeDef || token.TokenType == CorTokenType.mdtTypeRef)
                {
                    _typeToRefTokens[ecmaType] = token;
                }
            }
            else if (instantiatedType == null)
            {
                throw new NotImplementedException(type.ToString());
            }
        }

        /// <summary>
        /// As of 8/20/2018, recursive propagation of type information through
        /// the composite signature tree is not needed for anything. We're adding
        /// a dummy class to clearly indicate what aspects of the resolver need
        /// changing if the propagation becomes necessary.
        /// </summary>
        private class DummyTypeInfo
        {
            public static DummyTypeInfo Instance = new DummyTypeInfo(); 
        }

        private class TokenResolverProvider : ISignatureTypeProvider<DummyTypeInfo, ModuleTokenResolver>
        {
            ModuleTokenResolver _resolver;

            EcmaModule _contextModule;

            public TokenResolverProvider(ModuleTokenResolver resolver, EcmaModule contextModule)
            {
                _resolver = resolver;
                _contextModule = contextModule;
            }

            public DummyTypeInfo GetArrayType(DummyTypeInfo elementType, ArrayShape shape)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetByReferenceType(DummyTypeInfo elementType)
            {
                throw new NotImplementedException();
            }

            public DummyTypeInfo GetFunctionPointerType(MethodSignature<DummyTypeInfo> signature)
            {
                throw new NotImplementedException();
            }

            public DummyTypeInfo GetGenericInstantiation(DummyTypeInfo genericType, ImmutableArray<DummyTypeInfo> typeArguments)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetGenericMethodParameter(ModuleTokenResolver genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public DummyTypeInfo GetGenericTypeParameter(ModuleTokenResolver genericContext, int index)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetModifiedType(DummyTypeInfo modifier, DummyTypeInfo unmodifiedType, bool isRequired)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetPinnedType(DummyTypeInfo elementType)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetPointerType(DummyTypeInfo elementType)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetSZArrayType(DummyTypeInfo elementType)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                // Type definition tokens outside of the versioning bubble are useless.
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                _resolver.AddModuleTokenForType((TypeDesc)_contextModule.GetObject(handle), new ModuleToken(_contextModule, handle));
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetTypeFromSpecification(MetadataReader reader, ReadyToRunCodegenNodeFactory genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }

            public DummyTypeInfo GetTypeFromSpecification(MetadataReader reader, ModuleTokenResolver genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }
        }
    }
}

