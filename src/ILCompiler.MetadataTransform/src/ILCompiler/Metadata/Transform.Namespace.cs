// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private Dictionary<NamespaceKey, NamespaceDefinition> _namespaceDefs = new Dictionary<NamespaceKey, NamespaceDefinition>();

        private NamespaceDefinition HandleNamespaceDefinition(Cts.ModuleDesc parentScope, string namespaceString)
        {
            NamespaceDefinition rootNamespace = HandleScopeDefinition(parentScope).RootNamespaceDefinition;

            if (String.IsNullOrEmpty(namespaceString))
            {
                return rootNamespace;
            }

            NamespaceDefinition result;
            NamespaceKey key = new NamespaceKey(parentScope, namespaceString);
            if (_namespaceDefs.TryGetValue(key, out result))
            {
                return result;
            }

            NamespaceDefinition currentNamespace = rootNamespace;
            string currentNamespaceName = String.Empty;
            foreach (var segment in namespaceString.Split('.'))
            {
                string nextNamespaceName = currentNamespaceName;
                if (nextNamespaceName.Length > 0)
                    nextNamespaceName = nextNamespaceName + '.';
                nextNamespaceName += segment;
                NamespaceDefinition nextNamespace;
                key = new NamespaceKey(parentScope, nextNamespaceName);
                if (!_namespaceDefs.TryGetValue(key, out nextNamespace))
                {
                    nextNamespace = new NamespaceDefinition
                    {
                        Name = HandleString(segment.Length == 0 ? null : segment),
                        ParentScopeOrNamespace = currentNamespace
                    };

                    _namespaceDefs.Add(key, nextNamespace);
                    currentNamespace.NamespaceDefinitions.Add(nextNamespace);
                }
                currentNamespace = nextNamespace;
                currentNamespaceName = nextNamespaceName;
            }

            return currentNamespace;
        }

        private Dictionary<NamespaceKey, NamespaceReference> _namespaceRefs = new Dictionary<NamespaceKey, NamespaceReference>();

        private NamespaceReference HandleNamespaceReference(Cts.ModuleDesc parentScope, string namespaceString)
        {
            throw new NotImplementedException();
        }
    }

    internal struct NamespaceKey : IEquatable<NamespaceKey>
    {
        public readonly Cts.ModuleDesc Module;
        public readonly string Namespace;

        public NamespaceKey(Cts.ModuleDesc module, string namespaceName)
        {
            Module = module;
            Namespace = namespaceName;
        }

        public bool Equals(NamespaceKey other)
        {
            return Module == other.Module && Namespace == other.Namespace;
        }

        public override bool Equals(object obj)
        {
            if (obj is NamespaceKey)
                return Equals((NamespaceKey)obj);
            return false;
        }

        public override int GetHashCode()
        {
            return Namespace.GetHashCode();
        }
    }
}
