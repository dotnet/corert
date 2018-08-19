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

        private readonly Dictionary<MethodDesc, ModuleToken> _methodToRefTokens = new Dictionary<MethodDesc, ModuleToken>();

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
                return new ModuleToken(ecmaMethod.Module, (mdToken)MetadataTokens.GetToken(ecmaMethod.Handle));
            }

            if (_methodToRefTokens.TryGetValue(method, out ModuleToken token))
            {
                return token;
            }

            // Reverse lookup failed
            throw new NotImplementedException(method.ToString());
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
                            typeSpec.DecodeSignature(new TokenResolverProvider(this, token.Module), this);
                        }
                        break;
                }
            }
            else
            {
                throw new NotImplementedException(type.ToString());
            }
        }

        private class TokenResolverProvider : ISignatureTypeProvider<bool, ModuleTokenResolver>
        {
            ModuleTokenResolver _resolver;

            EcmaModule _contextModule;

            public TokenResolverProvider(ModuleTokenResolver resolver, EcmaModule contextModule)
            {
                _resolver = resolver;
                _contextModule = contextModule;
            }

            public bool GetArrayType(bool elementType, ArrayShape shape)
            {
                throw new NotImplementedException();
            }

            public bool GetByReferenceType(bool elementType)
            {
                throw new NotImplementedException();
            }

            public bool GetFunctionPointerType(MethodSignature<bool> signature)
            {
                throw new NotImplementedException();
            }

            public bool GetGenericInstantiation(bool genericType, ImmutableArray<bool> typeArguments)
            {
                return true;
            }

            public bool GetGenericMethodParameter(ModuleTokenResolver genericContext, int index)
            {
                throw new NotImplementedException();
            }

            public bool GetGenericTypeParameter(ModuleTokenResolver genericContext, int index)
            {
                return false;
            }

            public bool GetModifiedType(bool modifier, bool unmodifiedType, bool isRequired)
            {
                return false;
            }

            public bool GetPinnedType(bool elementType)
            {
                return false;
            }

            public bool GetPointerType(bool elementType)
            {
                return false;
            }

            public bool GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return false;
            }

            public bool GetSZArrayType(bool elementType)
            {
                return false;
            }

            public bool GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                // Type definition tokens outside of the versioning bubble are useless.
                return false;
            }

            public bool GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                _resolver.AddModuleTokenForType((TypeDesc)_contextModule.GetObject(handle), new ModuleToken(_contextModule, handle));
                return true;
            }

            public bool GetTypeFromSpecification(MetadataReader reader, ReadyToRunCodegenNodeFactory genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }

            public bool GetTypeFromSpecification(MetadataReader reader, ModuleTokenResolver genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotImplementedException();
            }
        }
    }
}

