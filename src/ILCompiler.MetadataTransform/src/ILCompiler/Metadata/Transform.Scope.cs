// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    partial class Transform<TPolicy>
    {
        internal EntityMap<Cts.ModuleDesc, ScopeDefinition> _scopeDefs
            = new EntityMap<Cts.ModuleDesc, ScopeDefinition>(EqualityComparer<Cts.ModuleDesc>.Default);
        private Action<Cts.ModuleDesc, ScopeDefinition> _initScopeDef;

        private ScopeDefinition HandleScopeDefinition(Cts.ModuleDesc module)
        {
            return _scopeDefs.GetOrCreate(module, _initScopeDef ?? (_initScopeDef = InitializeScopeDefinition));
        }

        private void InitializeScopeDefinition(Cts.ModuleDesc module, ScopeDefinition scopeDefinition)
        {
            // Make sure we're expected to create a scope definition here. If the assert fires, the metadata
            // policy should have directed us to create a scope reference (or the list of inputs was incomplete).
            Debug.Assert(_modulesToTransform.Contains(module), "Incomplete list of input modules with respect to metadata policy");

            var assemblyDesc = module as Cts.IAssemblyDesc;
            if (assemblyDesc != null)
            {
                var assemblyName = assemblyDesc.GetName();

                scopeDefinition.Name = HandleString(assemblyName.Name);
#if NETFX_45
                // With NET 4.5 contract System.Reflection 4.0.0.0 EcmaModule has no way
                // to set Culture in its AssemblyName.
                scopeDefinition.Culture = HandleString(assemblyName.CultureName ?? "");
#else
                scopeDefinition.Culture = HandleString(assemblyName.CultureName);
#endif
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

        private EntityMap<Cts.ModuleDesc, ScopeReference> _scopeRefs
            = new EntityMap<Cts.ModuleDesc, ScopeReference>(EqualityComparer<Cts.ModuleDesc>.Default);
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
