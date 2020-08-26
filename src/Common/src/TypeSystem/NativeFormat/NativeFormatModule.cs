// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using AssemblyFlags = Internal.Metadata.NativeFormat.AssemblyFlags;

using Internal.TypeSystem;

namespace Internal.TypeSystem.NativeFormat
{
    /// <summary>
    /// Represent an module in the CLI sense. Note, that as the NativeFormat for metadata can aggregate
    /// multiple modules, this class represents a subset of a NativeFormat metadata file.
    /// </summary>
    public sealed class NativeFormatModule : ModuleDesc, IAssemblyDesc
    {
        private QualifiedScopeDefinition[] _assemblyDefinitions;

        public struct QualifiedScopeDefinition
        {
            public QualifiedScopeDefinition(NativeFormatMetadataUnit metadataUnit, Handle handle)
            {
                MetadataUnit = metadataUnit;
                Handle = handle.ToScopeDefinitionHandle(metadataUnit.MetadataReader);
                Definition = metadataUnit.MetadataReader.GetScopeDefinition(Handle);
            }

            public readonly NativeFormatMetadataUnit MetadataUnit;

            public readonly ScopeDefinitionHandle Handle;
            public readonly ScopeDefinition Definition;
            public MetadataReader MetadataReader
            {
                get
                {
                    return MetadataUnit.MetadataReader;
                }
            }
        }

        public override IAssemblyDesc Assembly
        {
            get
            {
                return this;
            }
        }

        public NativeFormatModule(TypeSystemContext context, QualifiedScopeDefinition[] assemblyDefinitions)
            : base(context, null)
        {
            _assemblyDefinitions = assemblyDefinitions;
        }

        private class ReferenceKeyValuePair<TKey, TValue>
        {
            public ReferenceKeyValuePair(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }
            public TKey Key;
            public TValue Value;
        }

        private struct QualifiedNamespaceDefinition
        {
            public QualifiedNamespaceDefinition(NativeFormatMetadataUnit metadataUnit, NamespaceDefinition namespaceDefinition)
            {
                MetadataUnit = metadataUnit;
                Definition = namespaceDefinition;
            }

            public readonly NativeFormatMetadataUnit MetadataUnit;
            public readonly NamespaceDefinition Definition;
            public MetadataReader MetadataReader
            {
                get
                {
                    return MetadataUnit.MetadataReader;
                }
            }
        }

        private class NamespaceDefinitionHashtable : LockFreeReaderHashtable<string, ReferenceKeyValuePair<string, QualifiedNamespaceDefinition[]>>
        {
            private NativeFormatModule _module;
            private QualifiedScopeDefinition[] _scopes;

            public NamespaceDefinitionHashtable(NativeFormatModule module, QualifiedScopeDefinition[] scopes)
            {
                _module = module;
                _scopes = scopes;
            }

            protected override int GetKeyHashCode(string key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(ReferenceKeyValuePair<string, QualifiedNamespaceDefinition[]> value)
            {
                return value.Key.GetHashCode();
            }

            protected override bool CompareKeyToValue(string key, ReferenceKeyValuePair<string, QualifiedNamespaceDefinition[]> value)
            {
                return key.Equals(value.Key);
            }

            protected override bool CompareValueToValue(ReferenceKeyValuePair<string, QualifiedNamespaceDefinition[]> value1, ReferenceKeyValuePair<string, QualifiedNamespaceDefinition[]> value2)
            {
                return value1.Key.Equals(value2.Key);
            }

            protected override ReferenceKeyValuePair<string, QualifiedNamespaceDefinition[]> CreateValueFromKey(string key)
            {
                ArrayBuilder<QualifiedNamespaceDefinition> namespaceDefinitions = new ArrayBuilder<QualifiedNamespaceDefinition>();

                foreach (QualifiedScopeDefinition qdefinition in _scopes)
                {
                    MetadataReader metadataReader = qdefinition.MetadataReader;

                    NamespaceDefinitionHandle rootNamespaceHandle = qdefinition.Definition.RootNamespaceDefinition;
                    NamespaceDefinition currentNamespace = metadataReader.GetNamespaceDefinition(rootNamespaceHandle);
                    if (key == "")
                    {
                        namespaceDefinitions.Add(new QualifiedNamespaceDefinition(qdefinition.MetadataUnit, currentNamespace));
                        // We're done now.
                    }
                    else
                    {
                        string[] components = key.Split('.');
                        bool found = false;
                        foreach (string namespaceComponent in components)
                        {
                            found = false;
                            foreach (NamespaceDefinitionHandle childNamespaceHandle in currentNamespace.NamespaceDefinitions)
                            {
                                NamespaceDefinition childNamespace = metadataReader.GetNamespaceDefinition(childNamespaceHandle);
                                if (childNamespace.Name.StringEquals(namespaceComponent, metadataReader))
                                {
                                    found = true;
                                    currentNamespace = childNamespace;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                break;
                            }
                        }

                        if (found)
                            namespaceDefinitions.Add(new QualifiedNamespaceDefinition(qdefinition.MetadataUnit, currentNamespace));
                    }
                }

                return new ReferenceKeyValuePair<System.String, QualifiedNamespaceDefinition[]>(key, namespaceDefinitions.ToArray());
            }
        }

        private NamespaceDefinitionHashtable _namespaceLookup;

        private QualifiedNamespaceDefinition[] GetNamespaceDefinitionsFromString(string nameSpace)
        {
            if (_namespaceLookup == null)
                _namespaceLookup = new NamespaceDefinitionHashtable(this, _assemblyDefinitions);

            return _namespaceLookup.GetOrCreateValue(nameSpace ?? "").Value;
        }

        public override MetadataType GetType(string nameSpace, string name, bool throwIfNotFound = true)
        {
            QualifiedNamespaceDefinition[] namespaceDefinitions = GetNamespaceDefinitionsFromString(nameSpace);

            foreach (QualifiedNamespaceDefinition namespaceDefinition in namespaceDefinitions)
            {
                // At least the namespace was found.
                MetadataReader metadataReader = namespaceDefinition.MetadataReader;

                // Now scan the type definitions on this namespace
                foreach (var typeDefinitionHandle in namespaceDefinition.Definition.TypeDefinitions)
                {
                    var typeDefinition = metadataReader.GetTypeDefinition(typeDefinitionHandle);
                    if (typeDefinition.Name.StringEquals(name, metadataReader))
                    {
                        return (MetadataType)namespaceDefinition.MetadataUnit.GetType((Handle)typeDefinitionHandle);
                    }
                }
            }

            foreach (QualifiedNamespaceDefinition namespaceDefinition in namespaceDefinitions)
            {
                // At least the namespace was found.
                MetadataReader metadataReader = namespaceDefinition.MetadataReader;
                
                // Now scan the type forwarders on this namespace
                foreach (var typeForwarderHandle in namespaceDefinition.Definition.TypeForwarders)
                {
                    var typeForwarder = metadataReader.GetTypeForwarder(typeForwarderHandle);
                    if (typeForwarder.Name.StringEquals(name, metadataReader))
                    {
                        ModuleDesc forwardTargetModule = namespaceDefinition.MetadataUnit.GetModule(typeForwarder.Scope);
                        return forwardTargetModule.GetType(nameSpace, name, throwIfNotFound);
                    }
                }
            }

            if (throwIfNotFound)
                throw CreateTypeLoadException(nameSpace + "." + name);

            return null;
        }

        /// <summary>
        /// Enumerate all type definitions in a given native module. Iterate all scopes
        /// and recursively enumerate the namespace trees and all namespace and nested types there.
        /// </summary>
        public override IEnumerable<MetadataType> GetAllTypes()
        {
            foreach (QualifiedScopeDefinition scopeDefinition in _assemblyDefinitions)
            {
                foreach (MetadataType type in GetTypesInNamespace(
                    scopeDefinition.MetadataUnit,
                    scopeDefinition.MetadataReader,
                    scopeDefinition.Definition.RootNamespaceDefinition))
                {
                    yield return type;
                }
            }
        }

        /// <summary>
        /// Enumerate all type definitions within a given namespace.
        /// </summary>
        /// <param name="metadataUnit">Metadata unit containing the namespace</param>
        /// <param name="metadataReader">Metadata reader for the scope metadata</param>
        /// <param name="namespaceDefinitionHandle">Namespace to enumerate</param>
        private IEnumerable<MetadataType> GetTypesInNamespace(
            NativeFormatMetadataUnit metadataUnit,
            MetadataReader metadataReader,
            NamespaceDefinitionHandle namespaceDefinitionHandle)
        {
            NamespaceDefinition namespaceDefinition = metadataReader.GetNamespaceDefinition(namespaceDefinitionHandle);

            // First, enumerate all types (including nested) in the current namespace
            foreach (TypeDefinitionHandle namespaceTypeHandle in namespaceDefinition.TypeDefinitions)
            {
                yield return (MetadataType)metadataUnit.GetType(namespaceTypeHandle);
                foreach (MetadataType nestedType in GetNestedTypes(metadataUnit, metadataReader, namespaceTypeHandle))
                {
                    yield return nestedType;
                }
            }

            // Second, recurse into nested namespaces
            foreach (NamespaceDefinitionHandle nestedNamespace in namespaceDefinition.NamespaceDefinitions)
            {
                foreach (MetadataType type in GetTypesInNamespace(metadataUnit, metadataReader, nestedNamespace))
                {
                    yield return type;
                }
            }
        }

        /// <summary>
        /// Enumerate all nested types under a given owning type.
        /// </summary>
        /// <param name="metadataUnit">Metadata unit containing the type</param>
        /// <param name="metadataReader">Metadata reader for the type metadata</param>
        /// <param name="owningTypeHandle">Containing type to search for nested types</param>
        private IEnumerable<MetadataType> GetNestedTypes(
            NativeFormatMetadataUnit metadataUnit,
            MetadataReader metadataReader,
            TypeDefinitionHandle owningTypeHandle)
        {
            TypeDefinition owningType = metadataReader.GetTypeDefinition(owningTypeHandle);
            foreach (TypeDefinitionHandle nestedTypeHandle in owningType.NestedTypes)
            {
                yield return (MetadataType)metadataUnit.GetType(nestedTypeHandle);
                foreach (MetadataType nestedType in GetNestedTypes(metadataUnit, metadataReader, nestedTypeHandle))
                {
                    yield return nestedType;
                }
            }
        }

        public Exception CreateTypeLoadException(string fullTypeName)
        {
            return new TypeLoadException(String.Format("Could not load type '{0}' from assembly '{1}'.", fullTypeName, this.ToString()));
        }

        public override MetadataType GetGlobalModuleType()
        {
            return null;
            // TODO handle the global module type.
        }

        private AssemblyName _assemblyName;

        // Returns cached copy of the name. Caller has to create a clone before mutating the name.
        public AssemblyName GetName()
        {
            if (_assemblyName == null)
            {
                MetadataReader metadataReader = _assemblyDefinitions[0].MetadataReader;
                ScopeDefinition definition = _assemblyDefinitions[0].Definition;

                AssemblyName an = new AssemblyName();
                an.Name = metadataReader.GetString(definition.Name);
                an.Version = new Version(definition.MajorVersion, definition.MinorVersion, definition.BuildNumber, definition.RevisionNumber);
                byte[] publicKeyOrToken = definition.PublicKey.ConvertByteCollectionToArray();
                if ((definition.Flags & AssemblyFlags.PublicKey) != 0)
                {
                    an.SetPublicKey(publicKeyOrToken);
                }
                else
                {
                    an.SetPublicKeyToken(publicKeyOrToken);
                }

                // TODO: ContentType, Culture - depends on newer version of the System.Reflection contract

                _assemblyName = an;
            }

            return _assemblyName;
        }

        public bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            foreach (QualifiedScopeDefinition definition in _assemblyDefinitions)
            {
                if (definition.MetadataReader.HasCustomAttribute(definition.Definition.CustomAttributes,
                    attributeNamespace, attributeName))
                    return true;
            }

            return false;
        }

        public override string ToString()
        {
            return GetName().Name;
        }
    }
}
