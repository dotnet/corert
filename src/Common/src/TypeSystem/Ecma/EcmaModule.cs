// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed class EcmaModule
    {
        TypeSystemContext _context;

        PEReader _peReader;
        MetadataReader _metadataReader;

        AssemblyDefinition _assemblyDefinition;

        ImmutableDictionary<EntityHandle, Object> _resolvedTokens = ImmutableDictionary.Create<EntityHandle, object>();

        public EcmaModule(TypeSystemContext context, PEReader peReader)
        {
            _context = context;

            _peReader = peReader;

            var stringDecoderProvider = context as IMetadataStringDecoderProvider;

            _metadataReader = peReader.GetMetadataReader(MetadataReaderOptions.None /* MetadataReaderOptions.ApplyWindowsRuntimeProjections */,
                (stringDecoderProvider != null) ? stringDecoderProvider.GetMetadataStringDecoder() : null);

            _assemblyDefinition = _metadataReader.GetAssemblyDefinition();
        }

        public EcmaModule(TypeSystemContext context, MetadataReader metadataReader)
        {
            _context = context;

            _metadataReader = metadataReader;
        }

        public TypeSystemContext Context
        {
            get
            {
                return _context;
            }
        }

        public PEReader PEReader
        {
            get
            {
                return _peReader;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _metadataReader;
            }
        }

        public AssemblyDefinition AssemblyDefinition
        {
            get
            {
                return _assemblyDefinition;
            }
        }

        public MetadataType GetType(string nameSpace, string name, bool throwIfNotFound = true)
        {
            var stringComparer = _metadataReader.StringComparer;

            // TODO: More efficient implementation?
            foreach (var typeDefinitionHandle in _metadataReader.TypeDefinitions)
            {
                var typeDefinition = _metadataReader.GetTypeDefinition(typeDefinitionHandle);
                if (stringComparer.Equals(typeDefinition.Name, name) &&
                    stringComparer.Equals(typeDefinition.Namespace, nameSpace))
                {
                    return (MetadataType)GetType((EntityHandle)typeDefinitionHandle);
                }                
            }

            foreach (var exportedTypeHandle in _metadataReader.ExportedTypes)
            {
                var exportedType = _metadataReader.GetExportedType(exportedTypeHandle);
                if (stringComparer.Equals(exportedType.Name, name) &&
                    stringComparer.Equals(exportedType.Namespace, nameSpace))
                {
                    if (exportedType.IsForwarder)
                    {
                        Object implementation = GetObject(exportedType.Implementation);
                        
                        if (implementation is EcmaModule)
                        {
                            return ((EcmaModule)(implementation)).GetType(nameSpace, name);
                        }

                        // TODO
                        throw new NotImplementedException();
                    }
                    // TODO:
                    throw new NotImplementedException();
                }                
            }

            if (throwIfNotFound)
                throw CreateTypeLoadException(nameSpace + "." + name);

            return null;
        }

        public Exception CreateTypeLoadException(string fullTypeName)
        {
            return new TypeLoadException(String.Format("Could not load type '{0}' from assembly '{1}'.", fullTypeName, this.ToString()));
        }

        public TypeDesc GetType(EntityHandle handle)
        {
            TypeDesc type = GetObject(handle) as TypeDesc;
            if (type == null)
                throw new BadImageFormatException("Type expected");
            return type;
        }

        public MethodDesc GetMethod(EntityHandle handle)
        {
            MethodDesc method = GetObject(handle) as MethodDesc;
            if (method == null)
                throw new BadImageFormatException("Method expected");
            return method;
        }

        public FieldDesc GetField(EntityHandle handle)
        {
            FieldDesc field = GetObject(handle) as FieldDesc;
            if (field == null)
                throw new BadImageFormatException("Field expected");
            return field;
        }

        public Object GetObject(EntityHandle handle)
        {
            Object existingItem;
            if (_resolvedTokens.TryGetValue(handle, out existingItem))
                return existingItem;

            return CreateObject(handle);
        }

        private Object CreateObject(EntityHandle handle)
        {
            Object item;
            switch (handle.Kind)
            {
            case HandleKind.TypeDefinition:
                item = new EcmaType(this, (TypeDefinitionHandle)handle);
                break;
                
            case HandleKind.MethodDefinition:
                {
                    MethodDefinitionHandle methodDefinitionHandle = (MethodDefinitionHandle)handle;
                    TypeDefinitionHandle typeDefinitionHandle = _metadataReader.GetMethodDefinition(methodDefinitionHandle).GetDeclaringType();
                    EcmaType type = (EcmaType)GetObject(typeDefinitionHandle);
                    item = new EcmaMethod(type, methodDefinitionHandle);
                }
                break;

            case HandleKind.FieldDefinition:
                {
                    FieldDefinitionHandle fieldDefinitionHandle = (FieldDefinitionHandle)handle;
                    TypeDefinitionHandle typeDefinitionHandle = _metadataReader.GetFieldDefinition(fieldDefinitionHandle).GetDeclaringType();
                    EcmaType type = (EcmaType)GetObject(typeDefinitionHandle);
                    item = new EcmaField(type, fieldDefinitionHandle);
                }
                break;

            case HandleKind.TypeReference:
                item = ResolveTypeReference((TypeReferenceHandle)handle);
                break;

            case HandleKind.MemberReference:
                item = ResolveMemberReference((MemberReferenceHandle)handle);
                break;

            case HandleKind.AssemblyReference:
                item = ResolveAssemblyReference((AssemblyReferenceHandle)handle);
                break;

            case HandleKind.TypeSpecification:
                item = ResolveTypeSpecification((TypeSpecificationHandle)handle);
                break;

            case HandleKind.MethodSpecification:
                item = ResolveMethodSpecification((MethodSpecificationHandle)handle);
                break;

            case HandleKind.ExportedType:
                item = ResolveExportedType((ExportedTypeHandle)handle);
                break;

            // TODO: Resolve other tokens

            default:
                throw new BadImageFormatException("Unknown metadata token type");
            }

            lock (this)
            {
                Object existingItem;
                if (_resolvedTokens.TryGetValue(handle, out existingItem))
                    return existingItem;
                _resolvedTokens = _resolvedTokens.Add(handle, item);
            }

            return item;
        }

        Object ResolveMethodSpecification(MethodSpecificationHandle handle)
        {
            MethodSpecification methodSpecification = _metadataReader.GetMethodSpecification(handle);

            MethodDesc methodDef = GetMethod(methodSpecification.Method);

            BlobReader signatureReader = _metadataReader.GetBlobReader(methodSpecification.Signature);
            EcmaSignatureParser parser = new EcmaSignatureParser(this, signatureReader);

            TypeDesc[] instantiation = parser.ParseMethodSpecSignature();
            return _context.GetInstantiatedMethod(methodDef, new Instantiation(instantiation));
        }

        Object ResolveTypeSpecification(TypeSpecificationHandle handle)
        {
            TypeSpecification typeSpecification = _metadataReader.GetTypeSpecification(handle);

            BlobReader signatureReader = _metadataReader.GetBlobReader(typeSpecification.Signature);
            EcmaSignatureParser parser = new EcmaSignatureParser(this, signatureReader);

            return parser.ParseType();
        }

        Object ResolveMemberReference(MemberReferenceHandle handle)
        {
            MemberReference memberReference = _metadataReader.GetMemberReference(handle);

            Object parent = GetObject(memberReference.Parent);

            if (parent is TypeDesc)
            {
                BlobReader signatureReader = _metadataReader.GetBlobReader(memberReference.Signature);

                EcmaSignatureParser parser = new EcmaSignatureParser(this, signatureReader);

                string name = _metadataReader.GetString(memberReference.Name);

                if (parser.IsFieldSignature)
                {
                    FieldDesc field = ((TypeDesc)parent).GetField(name);
                    if (field != null)
                        return field;

                    // TODO: Better error message
                    throw new MissingMemberException("Field not found " + parent.ToString() + "." + name);
                }
                else
                {
                    MethodDesc method = ((TypeDesc)parent).GetMethod(name, parser.ParseMethodSignature());
                    if (method != null)
                        return method;

                    // TODO: Lookup in parent
                    
                    // TODO: Better error message
                    throw new MissingMemberException("Method not found " + parent.ToString() + "." + name);
                }
            }

            // TODO: Not implemented
            throw new NotImplementedException();
        }

        Object ResolveTypeReference(TypeReferenceHandle handle)
        {
            TypeReference typeReference = _metadataReader.GetTypeReference(handle);

            Object resolutionScope = GetObject(typeReference.ResolutionScope);

            if (resolutionScope is EcmaModule)
            {
                return ((EcmaModule)(resolutionScope)).GetType(_metadataReader.GetString(typeReference.Namespace), _metadataReader.GetString(typeReference.Name));
            }
            else
            if (resolutionScope is EcmaType)
            {
                return ((EcmaType)(resolutionScope)).GetNestedType(_metadataReader.GetString(typeReference.Name));
            }

            // TODO
            throw new NotImplementedException();
        }

        Object ResolveAssemblyReference(AssemblyReferenceHandle handle)
        {
            AssemblyReference assemblyReference = _metadataReader.GetAssemblyReference(handle);

            AssemblyName an = new AssemblyName();
            an.Name = _metadataReader.GetString(assemblyReference.Name);
            an.Version = assemblyReference.Version;

            var publicKeyOrToken = _metadataReader.GetBlobBytes(assemblyReference.PublicKeyOrToken);
            if ((an.Flags & AssemblyNameFlags.PublicKey) != 0)
            {
                an.SetPublicKey(publicKeyOrToken);
            }
            else
            {
                an.SetPublicKeyToken(publicKeyOrToken);
            }

            // TODO: ContentType, Culture - depends on newer version of the System.Reflection contract

            return _context.ResolveAssembly(an);
        }

        Object ResolveExportedType(ExportedTypeHandle handle)
        {
            ExportedType exportedType = _metadataReader.GetExportedType(handle);

            var implementation = GetObject(exportedType.Implementation);
            if (implementation is EcmaModule)
            {
                var module = (EcmaModule)implementation;
                string nameSpace = _metadataReader.GetString(exportedType.Namespace);
                string name = _metadataReader.GetString(exportedType.Name);
                return module.GetType(nameSpace, name);
            }
            else
            if (implementation is TypeDesc)
            {
                var type = (EcmaType)implementation;
                string name = _metadataReader.GetString(exportedType.Name);
                var nestedType = type.GetNestedType(name);
                // TODO: Better error message
                if (nestedType == null)
                    throw new TypeLoadException("Nested type not found " + type.ToString() + "." + name);
                return nestedType;
            }
            else
            {
                throw new BadImageFormatException("Unknown metadata token type for exported type");
            }
        }

        public IEnumerable<TypeDesc> GetAllTypes()
        {
            foreach (var typeDefinitionHandle in _metadataReader.TypeDefinitions)
            {
                yield return GetType(typeDefinitionHandle);
            }
        }

        public TypeDesc GetGlobalModuleType()
        {
            int typeDefinitionsCount = _metadataReader.TypeDefinitions.Count;
            if (typeDefinitionsCount == 0)
                return null;

            return GetType(MetadataTokens.EntityHandle(0x02000001 /* COR_GLOBAL_PARENT_TOKEN */));
        }

        AssemblyName _assemblyName;

        // Returns cached copy of the name. Caller has to create a clone before mutating the name.
        public AssemblyName GetName()
        {
            if (_assemblyName == null)
            {
                AssemblyName an = new AssemblyName();
                an.Name = _metadataReader.GetString(_assemblyDefinition.Name);
                an.Version = _assemblyDefinition.Version;
                an.SetPublicKey(_metadataReader.GetBlobBytes(_assemblyDefinition.PublicKey));

                // TODO: ContentType, Culture - depends on newer version of the System.Reflection contract

                _assemblyName = an;
            }

            return _assemblyName;
        }

        public string GetUserString(UserStringHandle userStringHandle)
        {
            // String literals are not cached
            return _metadataReader.GetUserString(userStringHandle);
        }

        internal bool HasCustomAttribute(CustomAttributeHandleCollection customAttributes, string customAttributeName)
        {
            foreach (var attributeHandle in customAttributes)
            {
                var customAttribute = _metadataReader.GetCustomAttribute(attributeHandle);
                var constructorHandle = customAttribute.Constructor;

                var constructor = GetMethod(constructorHandle);
                var type = constructor.OwningType;

                if (type.Name == customAttributeName)
                    return true;
            }

            return false;
        }

        public bool HasCustomAttribute(string customAttributeName)
        {
            return HasCustomAttribute(_assemblyDefinition.GetCustomAttributes(), customAttributeName);
        }

        public override string ToString()
        {
            return GetName().FullName;
        }
    }
}
