// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.General
{
    //
    // Since computation of the fullname and the declaring assembly both require walking up the namespace chain,
    // cache both results the first time we walk the chain.
    //
    internal sealed class NamespaceChain
    {
        internal NamespaceChain(MetadataReader reader, NamespaceDefinitionHandle innerMostNamespaceHandle)
        {
            NamespaceDefinition currentNamespaceDefinition = innerMostNamespaceHandle.GetNamespaceDefinition(reader);
            ConstantStringValueHandle currentNameHandle = currentNamespaceDefinition.Name;
            Handle currentNamespaceHandle = innerMostNamespaceHandle.ToHandle(reader);
            List<String> names = new List<String>();
            for (;;)
            {
                String name = currentNameHandle.GetStringOrNull(reader);
                names.Add(name);
                currentNamespaceHandle = currentNamespaceDefinition.ParentScopeOrNamespace;
                HandleType handleType = currentNamespaceHandle.HandleType;
                if (handleType == HandleType.ScopeDefinition)
                    break;
                if (handleType == HandleType.NamespaceDefinition)
                {
                    NamespaceDefinitionHandle nsHandle = currentNamespaceHandle.ToNamespaceDefinitionHandle(reader);
                    currentNamespaceDefinition = nsHandle.GetNamespaceDefinition(reader);
                    currentNameHandle = currentNamespaceDefinition.Name;
                    continue;
                }

                throw new BadImageFormatException(SR.Bif_InvalidMetadata);
            }

            DefiningScope = currentNamespaceHandle.ToScopeDefinitionHandle(reader);

            int count = names.Count;
            if (count == 0)
            {
                // Every namespace chain has to start with the root namespace.
                throw new BadImageFormatException();
            }
            else if (count == 1)
            {
                // The root namespace. For compat with the desktop, TypeInfo.NameSpaces returns null in this case.
                NameSpace = null;
            }
            else
            {
                // Namespace has at least one non-root component.
                StringBuilder sb = new StringBuilder();
                int idx = count - 1;
                while (idx-- != 0)
                {
                    String name = names[idx];
                    if (name == null)
                        throw new BadImageFormatException(); // null namespace fragment found in middle.
                    sb.Append(name);
                    if (idx != 0)
                        sb.Append('.');
                }
                NameSpace = sb.ToString();
            }
        }

        internal String NameSpace { get; }
        internal ScopeDefinitionHandle DefiningScope { get; }
    }
}

