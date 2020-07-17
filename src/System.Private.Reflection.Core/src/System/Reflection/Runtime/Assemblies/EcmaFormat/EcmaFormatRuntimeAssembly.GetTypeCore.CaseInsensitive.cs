// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.Modules;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.TypeParsing;
using System.Reflection.Runtime.CustomAttributes;
using System.Collections.Generic;

using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;

using Internal.Reflection.Tracing;

using System.Reflection.Metadata;

namespace System.Reflection.Runtime.Assemblies.EcmaFormat
{
    internal partial class EcmaFormatRuntimeAssembly
    {
        internal sealed override RuntimeTypeInfo GetTypeCoreCaseInsensitive(string fullName)
        {
            LowLevelDictionary<string, Handle> dict = CaseInsensitiveTypeDictionary;
            Handle typeDefOrForwarderHandle;
            if (!dict.TryGetValue(fullName.ToLowerInvariant(), out typeDefOrForwarderHandle))
            {
                return null;
            }

            MetadataReader reader = MetadataReader;

            HandleKind handleType = typeDefOrForwarderHandle.Kind;
            switch (handleType)
            {
                case HandleKind.TypeDefinition:
                    {
                        TypeDefinitionHandle typeDefinitionHandle = (TypeDefinitionHandle)typeDefOrForwarderHandle;
                        throw new NotImplementedException();
//                        return typeDefinitionHandle.ResolveTypeDefinition(reader);
                    }
                case HandleKind.ExportedType:
                    {
                        throw new NotImplementedException();
                        /*TypeForwarder typeForwarder = typeDefOrForwarderHandle.ToTypeForwarderHandle(reader).GetTypeForwarder(reader);
                        ScopeReferenceHandle destinationScope = typeForwarder.Scope;
                        RuntimeAssemblyName destinationAssemblyName = destinationScope.ToRuntimeAssemblyName(reader);
                        RuntimeAssembly destinationAssembly = RuntimeAssembly.GetRuntimeAssemblyIfExists(destinationAssemblyName);
                        if (destinationAssembly == null)
                            return null;
                        return destinationAssembly.GetTypeCoreCaseInsensitive(fullName);*/
                    }
                default:
                    throw new InvalidOperationException();
            }
        }

        private LowLevelDictionary<string, Handle> CaseInsensitiveTypeDictionary
        {
            get
            {
                return _lazyCaseInsensitiveTypeDictionary ?? (_lazyCaseInsensitiveTypeDictionary = CreateCaseInsensitiveTypeDictionary());
            }
        }

        private LowLevelDictionary<string, Handle> CreateCaseInsensitiveTypeDictionary()
        {
            //
            // Collect all of the *non-nested* types and type-forwards. 
            //
            //   The keys are full typenames in lower-cased form.
            //   The value is a tuple containing either a TypeDefinitionHandle or TypeForwarderHandle and the associated Reader
            //      for that handle.
            //
            // We do not store nested types here. The container type is resolved and chosen first, then the nested type chosen from 
            // that. If we chose the wrong container type and fail the match as a result, that's too bad. (The desktop CLR has the
            // same issue.)
            //

            LowLevelDictionary<string, Handle> dict = new LowLevelDictionary<string, Handle>();

            foreach (TypeDefinitionHandle typeDefinitionHandle in MetadataReader.TypeDefinitions)
            {
                TypeDefinition typeDefinition = MetadataReader.GetTypeDefinition(typeDefinitionHandle);
                string typeName = MetadataReader.GetString(typeDefinition.Name);
                string typeNamespace = MetadataReader.GetString(typeDefinition.NamespaceDefinition);
                string fullName = typeName;
                if (!String.IsNullOrEmpty(typeNamespace))
                {
                    fullName = typeNamespace + "." + typeName;
                }
                Handle existingValue;
                if (!dict.TryGetValue(fullName, out existingValue))
                {
                    dict.Add(fullName, typeDefinitionHandle);
                }
            }

            // TODO! Implement type forwarding logic.
            /*
                    foreach (TypeForwarderHandle typeForwarderHandle in namespaceDefinition.TypeForwarders)
                    {
                        string fullName = ns + typeForwarderHandle.GetTypeForwarder(reader).Name.GetString(reader).ToLowerInvariant();
                        QHandle existingValue;
                        if (!dict.TryGetValue(fullName, out existingValue))
                        {
                            dict.Add(fullName, new QHandle(reader, typeForwarderHandle));
                        }
                    }
                }*/

            return dict;
        }

        private volatile LowLevelDictionary<string, Handle> _lazyCaseInsensitiveTypeDictionary;
    }
}
