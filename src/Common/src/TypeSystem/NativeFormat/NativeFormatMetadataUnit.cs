// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Reflection;

using Internal.Metadata.NativeFormat;
using Internal.Runtime.Augments;
using Internal.TypeSystem;
using Internal.Reflection.Execution;
using Internal.Reflection.Core;
using Internal.Runtime;
using Internal.Runtime.TypeLoader;

using AssemblyFlags = Internal.Metadata.NativeFormat.AssemblyFlags;

namespace Internal.TypeSystem.NativeFormat
{
    /// <summary>
    /// Represent a NativeFormat metadata file. These files can contain multiple logical ECMA metadata style assemblies.
    /// </summary>
    public sealed class NativeFormatMetadataUnit
    {
        private NativeFormatModuleInfo _module;
        private MetadataReader _metadataReader;
        private TypeSystemContext _context;

        internal interface IHandleObject
        {
            Handle Handle
            {
                get;
            }

            NativeFormatType Container
            {
                get;
            }
        }

        private sealed class NativeFormatObjectLookupWrapper : IHandleObject
        {
            private Handle _handle;
            private object _obj;
            private NativeFormatType _container;

            public NativeFormatObjectLookupWrapper(Handle handle, object obj, NativeFormatType container)
            {
                _obj = obj;
                _handle = handle;
                _container = container;
            }

            public Handle Handle
            {
                get
                {
                    return _handle;
                }
            }

            public object Object
            {
                get
                {
                    return _obj;
                }
            }

            public NativeFormatType Container
            {
                get
                {
                    return _container;
                }
            }
        }

        internal struct NativeFormatObjectKey
        {
            private Handle _handle;
            private NativeFormatType _container;

            public NativeFormatObjectKey(Handle handle, NativeFormatType container)
            {
                _handle = handle;
                _container = container;
            }

            public Handle Handle { get { return _handle; } }
            public NativeFormatType Container { get { return _container; } }
        }

        internal class NativeFormatObjectLookupHashtable : LockFreeReaderHashtable<NativeFormatObjectKey, IHandleObject>
        {
            private NativeFormatMetadataUnit _metadataUnit;
            private MetadataReader _metadataReader;

            public NativeFormatObjectLookupHashtable(NativeFormatMetadataUnit metadataUnit, MetadataReader metadataReader)
            {
                _metadataUnit = metadataUnit;
                _metadataReader = metadataReader;
            }

            protected override int GetKeyHashCode(NativeFormatObjectKey key)
            {
                int hashcode = key.Handle.GetHashCode();
                if (key.Container != null)
                {
                    // Todo: Use a better hash combining function
                    hashcode ^= key.Container.GetHashCode();
                }

                return hashcode;
            }

            protected override int GetValueHashCode(IHandleObject value)
            {
                int hashcode = value.Handle.GetHashCode();
                if (value.Container != null)
                {
                    // Todo: Use a better hash combining function
                    hashcode ^= value.Container.GetHashCode();
                }

                return hashcode;
            }

            protected override bool CompareKeyToValue(NativeFormatObjectKey key, IHandleObject value)
            {
                return key.Handle.Equals(value.Handle) && Object.ReferenceEquals(key.Container, value.Container);
            }

            protected override bool CompareValueToValue(IHandleObject value1, IHandleObject value2)
            {
                if (Object.ReferenceEquals(value1, value2))
                    return true;
                else
                    return value1.Handle.Equals(value2.Handle) && Object.ReferenceEquals(value1.Container, value2.Container);
            }

            protected override IHandleObject CreateValueFromKey(NativeFormatObjectKey key)
            {
                Handle handle = key.Handle;
                NativeFormatType container = key.Container;

                object item;
                switch (handle.HandleType)
                {
                    case HandleType.TypeDefinition:
                        item = new NativeFormatType(_metadataUnit, handle.ToTypeDefinitionHandle(_metadataReader));
                        break;

                    case HandleType.Method:
                        item = new NativeFormatMethod(container, handle.ToMethodHandle(_metadataReader));
                        break;

                    case HandleType.Field:
                        item = new NativeFormatField(container, handle.ToFieldHandle(_metadataReader));
                        break;

                    case HandleType.TypeReference:
                        item = _metadataUnit.ResolveTypeReference(handle.ToTypeReferenceHandle(_metadataReader));
                        break;

                    case HandleType.MemberReference:
                        item = _metadataUnit.ResolveMemberReference(handle.ToMemberReferenceHandle(_metadataReader));
                        break;

                    case HandleType.QualifiedMethod:
                        item = _metadataUnit.ResolveQualifiedMethod(handle.ToQualifiedMethodHandle(_metadataReader));
                        break;

                    case HandleType.QualifiedField:
                        item = _metadataUnit.ResolveQualifiedField(handle.ToQualifiedFieldHandle(_metadataReader));
                        break;

                    case HandleType.ScopeReference:
                        item = _metadataUnit.ResolveAssemblyReference(handle.ToScopeReferenceHandle(_metadataReader));
                        break;

                    case HandleType.ScopeDefinition:
                        {
                            ScopeDefinition scope = handle.ToScopeDefinitionHandle(_metadataReader).GetScopeDefinition(_metadataReader);
                            item = _metadataUnit.GetModuleFromAssemblyName(scope.Name.GetConstantStringValue(_metadataReader).Value);
                        }
                        break;

                    case HandleType.TypeSpecification:
                    case HandleType.TypeInstantiationSignature:
                    case HandleType.SZArraySignature:
                    case HandleType.ArraySignature:
                    case HandleType.PointerSignature:
                    case HandleType.ByReferenceSignature:
                    case HandleType.TypeVariableSignature:
                    case HandleType.MethodTypeVariableSignature:
                        {
                            NativeFormatSignatureParser parser = new NativeFormatSignatureParser(_metadataUnit, handle, _metadataReader);

                            item = parser.ParseTypeSignature();
                        }
                        break;

                    case HandleType.MethodInstantiation:
                        item = _metadataUnit.ResolveMethodInstantiation(handle.ToMethodInstantiationHandle(_metadataReader));
                        break;

                    // TODO: Resolve other tokens
                    default:
                        throw new BadImageFormatException("Unknown metadata token type: " + handle.HandleType);
                }

                switch (handle.HandleType)
                {
                    case HandleType.TypeDefinition:
                    case HandleType.Field:
                    case HandleType.Method:
                        // type/method/field definitions directly correspond to their target item.
                        return (IHandleObject)item;
                    default:
                        // Everything else is some form of reference which cannot be self-describing
                        return new NativeFormatObjectLookupWrapper(handle, item, container);
                }
            }
        }

        private NativeFormatObjectLookupHashtable _resolvedTokens;

        public NativeFormatMetadataUnit(TypeSystemContext context, NativeFormatModuleInfo module, MetadataReader metadataReader)
        {
            _module = module;
            _metadataReader = metadataReader;
            _context = context;

            _resolvedTokens = new NativeFormatObjectLookupHashtable(this, _metadataReader);
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _metadataReader;
            }
        }

        public TypeSystemContext Context
        {
            get
            {
                return _context;
            }
        }

        public TypeManagerHandle RuntimeModule
        {
            get
            {
                return _module.Handle;
            }
        }

        public NativeFormatModuleInfo RuntimeModuleInfo
        {
            get
            {
                return _module;
            }
        }

        public TypeDesc GetType(Handle handle)
        {
            TypeDesc type = GetObject(handle, null) as TypeDesc;
            if (type == null)
                throw new BadImageFormatException("Type expected");
            return type;
        }

        public MethodDesc GetMethod(Handle handle, NativeFormatType type)
        {
            MethodDesc method = GetObject(handle, type) as MethodDesc;
            if (method == null)
                throw new BadImageFormatException("Method expected");
            return method;
        }

        public FieldDesc GetField(Handle handle, NativeFormatType type)
        {
            FieldDesc field = GetObject(handle, type) as FieldDesc;
            if (field == null)
                throw new BadImageFormatException("Field expected");
            return field;
        }

        public ModuleDesc GetModule(ScopeReferenceHandle scopeHandle)
        {
            return (ModuleDesc)GetObject(scopeHandle, null);
        }

        public NativeFormatModule GetModule(ScopeDefinitionHandle scopeHandle)
        {
            return (NativeFormatModule)GetObject(scopeHandle, null);
        }

        public Object GetObject(Handle handle, NativeFormatType type)
        {
            IHandleObject obj = _resolvedTokens.GetOrCreateValue(new NativeFormatObjectKey(handle, type));
            if (obj is NativeFormatObjectLookupWrapper)
            {
                return ((NativeFormatObjectLookupWrapper)obj).Object;
            }
            else
            {
                return obj;
            }
        }

        private Object ResolveMethodInstantiation(MethodInstantiationHandle handle)
        {
            MethodInstantiation methodInstantiation = _metadataReader.GetMethodInstantiation(handle);
            MethodDesc genericMethodDef = (MethodDesc)GetObject(methodInstantiation.Method, null);
            ArrayBuilder<TypeDesc> instantiation = new ArrayBuilder<TypeDesc>();
            foreach (Handle genericArgHandle in methodInstantiation.GenericTypeArguments)
            {
                instantiation.Add(GetType(genericArgHandle));
            }
            return Context.GetInstantiatedMethod(genericMethodDef, new Instantiation(instantiation.ToArray()));
        }

        private Object ResolveQualifiedMethod(QualifiedMethodHandle handle)
        {
            QualifiedMethod qualifiedMethod = _metadataReader.GetQualifiedMethod(handle);
            NativeFormatType enclosingType = (NativeFormatType)GetType(qualifiedMethod.EnclosingType);
            return GetMethod(qualifiedMethod.Method, enclosingType);
        }

        private Object ResolveQualifiedField(QualifiedFieldHandle handle)
        {
            QualifiedField qualifiedField = _metadataReader.GetQualifiedField(handle);
            NativeFormatType enclosingType = (NativeFormatType)GetType(qualifiedField.EnclosingType);
            return GetField(qualifiedField.Field, enclosingType);
        }

        private Object ResolveMemberReference(MemberReferenceHandle handle)
        {
            MemberReference memberReference = _metadataReader.GetMemberReference(handle);

            TypeDesc parent = GetType(memberReference.Parent);

            TypeDesc parentTypeDesc = parent as TypeDesc;
            if (parentTypeDesc != null)
            {
                NativeFormatSignatureParser parser = new NativeFormatSignatureParser(this, memberReference.Signature, _metadataReader);

                string name = _metadataReader.GetString(memberReference.Name);

                if (parser.IsFieldSignature)
                {
                    FieldDesc field = parentTypeDesc.GetField(name);
                    if (field != null)
                        return field;

                    // TODO: Better error message
                    throw new MissingMemberException("Field not found " + parent.ToString() + "." + name);
                }
                else
                {
                    MethodSignature sig = parser.ParseMethodSignature();
                    TypeDesc typeDescToInspect = parentTypeDesc;

                    // Try to resolve the name and signature in the current type, or any of the base types.
                    do
                    {
                        // TODO: handle substitutions
                        MethodDesc method = typeDescToInspect.GetMethod(name, sig);
                        if (method != null)
                        {
                            // If this resolved to one of the base types, make sure it's not a constructor.
                            // Instance constructors are not inherited.
                            if (typeDescToInspect != parentTypeDesc && method.IsConstructor)
                                break;

                            return method;
                        }
                        typeDescToInspect = typeDescToInspect.BaseType;
                    } while (typeDescToInspect != null);

                    // TODO: Better error message
                    throw new MissingMemberException("Method not found " + parent.ToString() + "." + name);
                }
            }

            throw new BadImageFormatException();
        }

        private DefType ResolveTypeReference(TypeReferenceHandle handle)
        {
            TypeReference typeReference = _metadataReader.GetTypeReference(handle);

            if (typeReference.ParentNamespaceOrType.HandleType == HandleType.TypeReference)
            {
                // Nested type case
                MetadataType containingType = (MetadataType)ResolveTypeReference(typeReference.ParentNamespaceOrType.ToTypeReferenceHandle(_metadataReader));

                return containingType.GetNestedType(_metadataReader.GetString(typeReference.TypeName));
            }
            else
            {
                // Cross-assembly reference
                // Get remote module, and then lookup by namespace/name
                ScopeReferenceHandle scopeReferenceHandle = default(ScopeReferenceHandle);
                NamespaceReferenceHandle initialNamespaceReferenceHandle = typeReference.ParentNamespaceOrType.ToNamespaceReferenceHandle(_metadataReader);
                NamespaceReferenceHandle namespaceReferenceHandle = initialNamespaceReferenceHandle;
                do
                {
                    NamespaceReference namespaceReference = _metadataReader.GetNamespaceReference(namespaceReferenceHandle);
                    if (namespaceReference.ParentScopeOrNamespace.HandleType == HandleType.ScopeReference)
                    {
                        scopeReferenceHandle = namespaceReference.ParentScopeOrNamespace.ToScopeReferenceHandle(_metadataReader);
                    }
                    else
                    {
                        namespaceReferenceHandle = namespaceReference.ParentScopeOrNamespace.ToNamespaceReferenceHandle(_metadataReader);
                    }
                } while (scopeReferenceHandle.IsNull(_metadataReader));

                ModuleDesc remoteModule = GetModule(scopeReferenceHandle);

                string namespaceName = _metadataReader.GetNamespaceName(initialNamespaceReferenceHandle);
                string typeName = _metadataReader.GetString(typeReference.TypeName);

                MetadataType resolvedType = remoteModule.GetType(namespaceName, typeName, throwIfNotFound: false);
                if (resolvedType != null)
                {
                    return resolvedType;
                }

                // Special handling for the magic __Canon types cannot be currently put into
                // NativeFormatModule because GetType returns a MetadataType.
                if (remoteModule == _context.SystemModule)
                {
                    string qualifiedTypeName = namespaceName + "." + typeName;
                    if (qualifiedTypeName == CanonType.FullName)
                    {
                        return _context.CanonType;
                    }
                    if (qualifiedTypeName == UniversalCanonType.FullName)
                    {
                        return _context.UniversalCanonType;
                    }
                }

                throw new NotImplementedException();
            }
        }

        private Object ResolveAssemblyReference(ScopeReferenceHandle handle)
        {
            ScopeReference assemblyReference = _metadataReader.GetScopeReference(handle);

            AssemblyName an = new AssemblyName();
            an.Name = _metadataReader.GetString(assemblyReference.Name);
            an.Version = new Version(assemblyReference.MajorVersion, assemblyReference.MinorVersion, assemblyReference.BuildNumber, assemblyReference.RevisionNumber);
            an.CultureName = _metadataReader.GetString(assemblyReference.Culture) ?? "";

            var publicKeyOrToken = assemblyReference.PublicKeyOrToken.ConvertByteCollectionToArray();
            if ((assemblyReference.Flags & AssemblyFlags.PublicKey) != 0)
            {
                an.SetPublicKey(publicKeyOrToken);
            }
            else
            {
                an.SetPublicKeyToken(publicKeyOrToken);
            }

            // TODO: ContentType - depends on newer version of the System.Reflection contract

            return Context.ResolveAssembly(an);
        }

        public NativeFormatModule GetModuleFromNamespaceDefinition(NamespaceDefinitionHandle handle)
        {
            while (true)
            {
                NamespaceDefinition namespaceDef = _metadataReader.GetNamespaceDefinition(handle);
                Handle parentScopeOrDefinitionHandle = namespaceDef.ParentScopeOrNamespace;
                if (parentScopeOrDefinitionHandle.HandleType == HandleType.ScopeDefinition)
                {
                    return (NativeFormatModule)GetObject(parentScopeOrDefinitionHandle, null);
                }
                else
                {
                    handle = parentScopeOrDefinitionHandle.ToNamespaceDefinitionHandle(_metadataReader);
                }
            }
        }

        public NativeFormatModule GetModuleFromAssemblyName(string assemblyNameString)
        {
            AssemblyBindResult bindResult;
            RuntimeAssemblyName assemblyName = AssemblyNameParser.Parse(assemblyNameString);
            Exception failureException;
            if (!AssemblyBinderImplementation.Instance.Bind(assemblyName, cacheMissedLookups: true, out bindResult, out failureException))
            {
                throw failureException;
            }

            var moduleList = Internal.Runtime.TypeLoader.ModuleList.Instance;
            NativeFormatModuleInfo primaryModule = moduleList.GetModuleInfoForMetadataReader(bindResult.Reader);
            // If this isn't the primary module, defer to that module to load the MetadataUnit
            if (primaryModule != _module)
            {
                return Context.ResolveMetadataUnit(primaryModule).GetModule(bindResult.ScopeDefinitionHandle);
            }

            // Setup arguments and allocate the NativeFormatModule
            ArrayBuilder<NativeFormatModule.QualifiedScopeDefinition> qualifiedScopes = new ArrayBuilder<NativeFormatModule.QualifiedScopeDefinition>();
            qualifiedScopes.Add(new NativeFormatModule.QualifiedScopeDefinition(this, bindResult.ScopeDefinitionHandle));

            foreach (QScopeDefinition scope in bindResult.OverflowScopes)
            {
                NativeFormatModuleInfo module = moduleList.GetModuleInfoForMetadataReader(scope.Reader);
                ScopeDefinitionHandle scopeHandle = scope.Handle;
                NativeFormatMetadataUnit metadataUnit = Context.ResolveMetadataUnit(module);
                qualifiedScopes.Add(new NativeFormatModule.QualifiedScopeDefinition(metadataUnit, scopeHandle));
            }

            return new NativeFormatModule(Context, qualifiedScopes.ToArray());
        }
    }
}
