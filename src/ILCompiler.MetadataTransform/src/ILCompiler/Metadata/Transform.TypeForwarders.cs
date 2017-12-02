// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.Metadata
{
    partial class Transform<TPolicy>
    {
        private void HandleTypeForwarders(Cts.Ecma.EcmaModule module)
        {
            foreach (var exportedTypeHandle in module.MetadataReader.ExportedTypes)
            {
                Ecma.ExportedType exportedType = module.MetadataReader.GetExportedType(exportedTypeHandle);
                if (exportedType.IsForwarder || exportedType.Implementation.Kind == Ecma.HandleKind.ExportedType)
                {
                    try
                    {
                        HandleTypeForwarder(module, exportedType);
                    }
                    catch (Cts.TypeSystemException)
                    {
                        // TODO: We should emit unresolvable type forwards instead of skipping these
                    }
                }
                else
                {
                    Debug.Assert(false, "Multi-module assemblies");
                }
            }
        }

        private TypeForwarder HandleTypeForwarder(Cts.Ecma.EcmaModule module, Ecma.ExportedType exportedType)
        {
            Ecma.MetadataReader reader = module.MetadataReader;
            string name = reader.GetString(exportedType.Name);
            TypeForwarder result;

            switch (exportedType.Implementation.Kind)
            {
                case Ecma.HandleKind.AssemblyReference:
                    string ns = reader.GetString(exportedType.Namespace);
                    NamespaceDefinition namespaceDefinition = HandleNamespaceDefinition(module, ns);

                    result = new TypeForwarder
                    {
                        Name = HandleString(name),
                        Scope = HandleScopeReference((Cts.ModuleDesc)module.GetObject(exportedType.Implementation)),
                    };
                    
                    namespaceDefinition.TypeForwarders.Add(result);
                    break;

                case Ecma.HandleKind.ExportedType:
                    TypeForwarder scope = HandleTypeForwarder(module, reader.GetExportedType((Ecma.ExportedTypeHandle)exportedType.Implementation));

                    result = new TypeForwarder
                    {
                        Name = HandleString(name),
                        Scope = scope.Scope,
                    };

                    scope.NestedTypes.Add(result);
                    break;

                default:
                    throw new BadImageFormatException();
            }

            return result;
        }
    }
}
