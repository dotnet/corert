// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
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

using System.Reflection.Metadata;

using Internal.Reflection.Tracing;

namespace System.Reflection.Runtime.Assemblies.EcmaFormat
{
    internal partial class EcmaFormatRuntimeAssembly
    {
        internal sealed override RuntimeTypeInfo UncachedGetTypeCoreCaseSensitive(string fullName)
        {
            foreach (TypeDefinitionHandle typeDefinitionHandle in MetadataReader.TypeDefinitions)
            {
                TypeDefinition typeDefinition = MetadataReader.GetTypeDefinition(typeDefinitionHandle);
                string typeName = MetadataReader.GetString(typeDefinition.Name);
                string typeNamespace = MetadataReader.GetString(typeDefinition.NamespaceDefinition);
                string typeFullName = typeName;
                if (!String.IsNullOrEmpty(typeNamespace))
                {
                    fullName = typeNamespace + "." + typeName;
                }
                // TODO! Add a cache here so that we don't actually have to scan every type each time the runtime
                // loads a type.
                if (fullName.Equals(typeFullName))
                {
                    throw new NotImplementedException();
                    // TODO! Add logic to load a type
                }
            }

            // No match found in this assembly - see if there's a matching type forwarder.
            // TODO! Implement
            return null;
        }
    }
}
