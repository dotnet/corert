// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

using Debug = System.Diagnostics.Debug;
using MethodSemanticsAttributes = Internal.Metadata.NativeFormat.MethodSemanticsAttributes;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private Property HandleProperty(Cts.Ecma.EcmaModule module, Ecma.PropertyDefinitionHandle property)
        {
            Ecma.MetadataReader reader = module.MetadataReader;

            Ecma.PropertyDefinition propDef = reader.GetPropertyDefinition(property);
            Ecma.BlobReader sigBlobReader = reader.GetBlobReader(propDef.Signature);
            Ecma.SignatureHeader sigHeader = sigBlobReader.ReadSignatureHeader();

            Ecma.PropertyAccessors acc = propDef.GetAccessors();
            Cts.MethodDesc getterMethod = acc.Getter.IsNil ? null : module.GetMethod(acc.Getter);
            Cts.MethodDesc setterMethod = acc.Setter.IsNil ? null : module.GetMethod(acc.Setter);

            bool getterReflectable = getterMethod != null && _policy.GeneratesMetadata(getterMethod);
            bool setterReflectable = setterMethod != null && _policy.GeneratesMetadata(setterMethod);

            if (!getterReflectable && !setterReflectable)
                return null;

            Property result = new Property
            {
                Name = HandleString(reader.GetString(propDef.Name)),
                Flags = propDef.Attributes,
                Signature = new PropertySignature
                {
                    // TODO: CallingConvention
                    // TODO: CustomModifiers
                    // TODO: Parameters
                    // TODO: Type
                },
            };

            if (getterReflectable)
            {
                result.MethodSemantics.Add(new MethodSemantics
                {
                    Attributes = MethodSemanticsAttributes.Getter,
                    Method = HandleMethodDefinition(getterMethod),
                });
            }

            if (setterReflectable)
            {
                result.MethodSemantics.Add(new MethodSemantics
                {
                    Attributes = MethodSemanticsAttributes.Setter,
                    Method = HandleMethodDefinition(setterMethod),
                });
            }

            // TODO: DefaultValue
            // TODO: CustomAttributes

            return result;
        }

    }
}
