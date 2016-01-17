// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using System.Reflection.Metadata;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public enum SpecialMethodKind
    {
        Unknown,
        PInvoke,
        RuntimeImport
    };

    internal static class MethodExtensions
    {
        public static string GetAttributeStringValue(this EcmaMethod This, string nameSpace, string name)
        {
            var metadataReader = This.MetadataReader;
            foreach (var attributeHandle in metadataReader.GetMethodDefinition(This.Handle).GetCustomAttributes())
            {
                EntityHandle attributeType, attributeCtor;
                if (!metadataReader.GetAttributeTypeAndConstructor(attributeHandle,
                    out attributeType, out attributeCtor))
                {
                    continue;
                }

                StringHandle namespaceHandle, nameHandle;
                if (!metadataReader.GetAttributeTypeNamespaceAndName(attributeType,
                   out namespaceHandle, out nameHandle))
                {
                    continue;
                }

                if (metadataReader.StringComparer.Equals(namespaceHandle, nameSpace)
                    && metadataReader.StringComparer.Equals(nameHandle, name))
                {
                    var constructor = This.Module.GetMethod(attributeCtor);

                    if (constructor.Signature.Length != 1 && constructor.Signature.Length != 2)
                        throw new BadImageFormatException();

                    for (int i = 0; i < constructor.Signature.Length; i++)
                        if (constructor.Signature[i] != This.Context.GetWellKnownType(WellKnownType.String))
                            throw new BadImageFormatException();

                    var attributeBlob = metadataReader.GetBlobReader(metadataReader.GetCustomAttribute(attributeHandle).Value);
                    if (attributeBlob.ReadInt16() != 1)
                        throw new BadImageFormatException();

                    // Skip module name if present
                    if (constructor.Signature.Length == 2)
                        attributeBlob.ReadSerializedString();

                    return attributeBlob.ReadSerializedString();
                }
            }

            return null;
        }

        public static SpecialMethodKind DetectSpecialMethodKind(this MethodDesc method)
        {
            if (method.IsPInvoke)
            {
                // Marshalling is never required for pregenerated interop code
                if (Internal.IL.McgInteropSupport.IsPregeneratedInterop(method))
                {
                    return SpecialMethodKind.PInvoke;
                }

                if (!Internal.IL.Stubs.PInvokeMarshallingILEmitter.RequiresMarshalling(method))
                {
                    return SpecialMethodKind.PInvoke;
                }
            }

            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                return SpecialMethodKind.RuntimeImport;
            }

            return SpecialMethodKind.Unknown;
        }
    }
}
