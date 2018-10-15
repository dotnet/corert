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

        private readonly CompilerTypeSystemContext _typeSystemContext;

        public ModuleTokenResolver(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext)
        {
            _compilationModuleGroup = compilationModuleGroup;
            _typeSystemContext = typeSystemContext;
        }

        public ModuleToken GetModuleTokenForType(EcmaType type, bool throwIfNotFound = true)
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
            if (throwIfNotFound)
            {
                throw new NotImplementedException(type.ToString());
            }
            else
            {
                return default(ModuleToken);
            }
        }

        public ModuleToken GetModuleTokenForMethod(MethodDesc method, bool throwIfNotFound = true)
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
            if (throwIfNotFound)
            {
                throw new NotImplementedException(method.ToString());
            }
            else
            {
                return default(ModuleToken);
            }
        }

        public ModuleToken GetModuleTokenForField(FieldDesc field, bool throwIfNotFound = true)
        {
            if (_compilationModuleGroup.ContainsType(field.OwningType) && field is EcmaField ecmaField)
            {
                return new ModuleToken(ecmaField.Module, ecmaField.Handle);
            }

            if (throwIfNotFound)
            {
                throw new NotImplementedException();
            }
            else
            {
                return default(ModuleToken);
            }
        }

        public void AddModuleTokenForMethod(MethodDesc method, ModuleToken token)
        {
            // Always remove method instantiation as methodSpec's cannot be used to encode R2R method signatures
            method = method.GetMethodDefinition();
            if (token.TokenType == CorTokenType.mdtMethodSpec)
            {
                MethodSpecification methodSpec = token.MetadataReader.GetMethodSpecification((MethodSpecificationHandle)token.Handle);
                token = new ModuleToken(token.Module, methodSpec.Method);
            }

            if (_methodToRefTokens.ContainsKey(method))
            {
                // This method has already been harvested
                return;
            }

            if (_compilationModuleGroup.ContainsMethodBody(method, unboxingStub: false) && method is EcmaMethod)
            {
                // We don't need to store handles within the current compilation group
                // as we can read them directly from the ECMA objects.
                return;
            }

            _methodToRefTokens[method] = token;

            switch (token.TokenType)
            {
                case CorTokenType.mdtMemberRef:
                    if (method.OwningType.HasInstantiation && !method.OwningType.IsGenericDefinition)
                    {
                        AddModuleTokenForMethod(method.GetTypicalMethodDefinition(), token);
                    }
                    AddModuleTokenForMethodReference(method.OwningType, token);
                    break;

                default:
                    throw new NotImplementedException(token.TokenType.ToString());
            }
        }

        private void AddModuleTokenForMethodReference(TypeDesc owningType, ModuleToken token)
        {
            MemberReference memberRef = token.MetadataReader.GetMemberReference((MemberReferenceHandle)token.Handle);
            EntityHandle owningTypeHandle = memberRef.Parent;
            AddModuleTokenForType(owningType, new ModuleToken(token.Module, owningTypeHandle));

            memberRef.DecodeMethodSignature<DummyTypeInfo, ModuleTokenResolver>(new TokenResolverProvider(this, token.Module), this);
        }

        private void AddModuleTokenForFieldReference(TypeDesc owningType, ModuleToken token)
        {
            MemberReference memberRef = token.MetadataReader.GetMemberReference((MemberReferenceHandle)token.Handle);
            EntityHandle owningTypeHandle = memberRef.Parent;
            AddModuleTokenForType(owningType, new ModuleToken(token.Module, owningTypeHandle));
            memberRef.DecodeFieldSignature<DummyTypeInfo, ModuleTokenResolver>(new TokenResolverProvider(this, token.Module), this);
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
                    AddModuleTokenForFieldReference(field.OwningType, token);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public void AddModuleTokenForType(TypeDesc type, ModuleToken token)
        {
            bool specialTypeFound = false;
            // Collect underlying type tokens for type specifications
            if (token.TokenType == CorTokenType.mdtTypeSpec)
            {
                TypeSpecification typeSpec = token.MetadataReader.GetTypeSpecification((TypeSpecificationHandle)token.Handle);
                typeSpec.DecodeSignature(new TokenResolverProvider(this, token.Module), this);
                specialTypeFound = true;
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
            else if (!specialTypeFound)
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
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetFunctionPointerType(MethodSignature<DummyTypeInfo> signature)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetGenericInstantiation(DummyTypeInfo genericType, ImmutableArray<DummyTypeInfo> typeArguments)
            {
                return DummyTypeInfo.Instance;
            }

            public DummyTypeInfo GetGenericMethodParameter(ModuleTokenResolver genericContext, int index)
            {
                return DummyTypeInfo.Instance;
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

            public DummyTypeInfo GetTypeFromSpecification(MetadataReader reader, ModuleTokenResolver genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }
        }
    }
}

