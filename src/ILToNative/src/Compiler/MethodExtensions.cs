// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILToNative
{
    static class MethodExtensions
    {
        const string RuntimeImportAttributeName = "System.Runtime.RuntimeImportAttribute";

        public static bool IsRuntimeImport(this EcmaMethod This)
        {
            return This.HasCustomAttribute(RuntimeImportAttributeName);
        }

        public static string GetRuntimeImportEntryPointName(this EcmaMethod This)
        {
            var metadataReader = This.MetadataReader;
            foreach (var attributeHandle in metadataReader.GetMethodDefinition(This.Handle).GetCustomAttributes())
            {
                var customAttribute = metadataReader.GetCustomAttribute(attributeHandle);
                var constructorHandle = customAttribute.Constructor;

                var constructor = This.Module.GetMethod(constructorHandle);
                var type = constructor.OwningType;

                if (type.Name == RuntimeImportAttributeName)
                {
                    if (constructor.Signature.Length != 1 && constructor.Signature.Length != 2)
                        throw new BadImageFormatException();

                    for (int i = 0; i < constructor.Signature.Length; i++)
                        if (constructor.Signature[i] != This.Context.GetWellKnownType(WellKnownType.String))
                            throw new BadImageFormatException();

                    var attributeBlob = metadataReader.GetBlobReader(customAttribute.Value);
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

    }
}
