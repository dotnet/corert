// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using AssemblyFlags = Internal.Metadata.NativeFormat.AssemblyFlags;
using AssemblyNameFlags = System.Reflection.AssemblyNameFlags;
using AssemblyContentType = System.Reflection.AssemblyContentType;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private EntityMap<Cts.ModuleDesc, ScopeDefinition> _scopeDefs
            = new EntityMap<Cts.ModuleDesc, ScopeDefinition>(EqualityComparer<Cts.ModuleDesc>.Default);
        private Action<Cts.ModuleDesc, ScopeDefinition> _initScopeDef;

        private ScopeDefinition HandleScopeDefinition(Cts.ModuleDesc module)
        {
            return _scopeDefs.GetOrCreate(module, _initScopeDef ?? (_initScopeDef = InitializeScopeDefinition));
        }

        private void InitializeScopeDefinition(Cts.ModuleDesc module, ScopeDefinition scopeDefinition)
        {
            var assemblyDesc = module as Cts.IAssemblyDesc;
            if (assemblyDesc != null)
            {
                var assemblyName = assemblyDesc.GetName();

                scopeDefinition.Name = HandleString(assemblyName.Name);
                scopeDefinition.Culture = HandleString(assemblyName.CultureName);
                scopeDefinition.MajorVersion = checked((ushort)assemblyName.Version.Major);
                scopeDefinition.MinorVersion = checked((ushort)assemblyName.Version.Minor);
                scopeDefinition.BuildNumber = checked((ushort)assemblyName.Version.Build);
                scopeDefinition.RevisionNumber = checked((ushort)assemblyName.Version.Revision);

                Debug.Assert((int)AssemblyFlags.PublicKey == (int)AssemblyNameFlags.PublicKey);
                Debug.Assert((int)AssemblyFlags.Retargetable == (int)AssemblyNameFlags.Retargetable);
                scopeDefinition.Flags = (AssemblyFlags)assemblyName.Flags;

                if (assemblyName.ContentType == AssemblyContentType.WindowsRuntime)
                {
                    scopeDefinition.Flags |= (AssemblyFlags)((int)AssemblyContentType.WindowsRuntime << 9);
                }

                scopeDefinition.PublicKey = assemblyName.GetPublicKey();

                // TODO: CustomAttributes
            }
            else
            {
                throw new NotSupportedException("Multi-module assemblies");
            }

            scopeDefinition.RootNamespaceDefinition = new NamespaceDefinition
            {
                Name = null,
                ParentScopeOrNamespace = scopeDefinition,
            };
        }

        private EntityMap<Cts.ModuleDesc, ScopeReference> _scopeRefs;
        private Action<Cts.ModuleDesc, ScopeReference> _initScopeRef;

        private ScopeReference HandleScopeReference(Cts.ModuleDesc module)
        {
            return _scopeRefs.GetOrCreate(module, _initScopeRef ?? (_initScopeRef = InitializeScopeReference));
        }

        private void InitializeScopeReference(Cts.ModuleDesc module, ScopeReference scopeReference)
        {
            var assemblyDesc = module as Cts.IAssemblyDesc;
            if (assemblyDesc != null)
            {
                var assemblyName = assemblyDesc.GetName();

                scopeReference.Name = HandleString(assemblyName.Name);
                scopeReference.Culture = HandleString(assemblyName.CultureName);
                scopeReference.MajorVersion = checked((ushort)assemblyName.Version.Major);
                scopeReference.MinorVersion = checked((ushort)assemblyName.Version.Minor);
                scopeReference.BuildNumber = checked((ushort)assemblyName.Version.Build);
                scopeReference.RevisionNumber = checked((ushort)assemblyName.Version.Revision);

                Debug.Assert((int)AssemblyFlags.PublicKey == (int)AssemblyNameFlags.PublicKey);
                Debug.Assert((int)AssemblyFlags.Retargetable == (int)AssemblyNameFlags.Retargetable);
                scopeReference.Flags = (AssemblyFlags)assemblyName.Flags;

                if (assemblyName.ContentType == AssemblyContentType.WindowsRuntime)
                {
                    scopeReference.Flags |= (AssemblyFlags)((int)AssemblyContentType.WindowsRuntime << 9);
                }

                if ((assemblyName.Flags & AssemblyNameFlags.PublicKey) != 0)
                    scopeReference.PublicKeyOrToken = assemblyName.GetPublicKey();
                else
                    scopeReference.PublicKeyOrToken = assemblyName.GetPublicKeyToken();
            }
            else
            {
                throw new NotSupportedException("Multi-module assemblies");
            }
        }
    }
}
