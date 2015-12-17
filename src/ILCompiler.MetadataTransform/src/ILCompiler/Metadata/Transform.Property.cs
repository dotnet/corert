﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

using MethodSemanticsAttributes = Internal.Metadata.NativeFormat.MethodSemanticsAttributes;
using CallingConventions = System.Reflection.CallingConventions;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private Property HandleProperty(Cts.Ecma.EcmaModule module, Ecma.PropertyDefinitionHandle property)
        {
            Ecma.MetadataReader reader = module.MetadataReader;

            Ecma.PropertyDefinition propDef = reader.GetPropertyDefinition(property);

            Ecma.PropertyAccessors acc = propDef.GetAccessors();
            Cts.MethodDesc getterMethod = acc.Getter.IsNil ? null : module.GetMethod(acc.Getter);
            Cts.MethodDesc setterMethod = acc.Setter.IsNil ? null : module.GetMethod(acc.Setter);

            bool getterHasMetadata = getterMethod != null && _policy.GeneratesMetadata(getterMethod);
            bool setterHasMetadata = setterMethod != null && _policy.GeneratesMetadata(setterMethod);

            // Policy: If neither the getter nor setter have metadata, property doesn't have metadata
            if (!getterHasMetadata && !setterHasMetadata)
                return null;

            Ecma.BlobReader sigBlobReader = reader.GetBlobReader(propDef.Signature);
            Cts.PropertySignature sig = new Cts.Ecma.EcmaSignatureParser(module, sigBlobReader).ParsePropertySignature();

            List<ParameterTypeSignature> parameters;
            if (sig.Length == 0)
            {
                parameters = null;
            }
            else
            {
                parameters = new List<ParameterTypeSignature>(sig.Length);
                for (int i = 0; i < parameters.Count; i++)
                    parameters.Add(HandleParameterTypeSignature(sig[i]));
            }

            Property result = new Property
            {
                Name = HandleString(reader.GetString(propDef.Name)),
                Flags = propDef.Attributes,
                Signature = new PropertySignature
                {
                    CallingConvention = sig.IsStatic ? CallingConventions.Standard : CallingConventions.HasThis,
                    // TODO: CustomModifiers
                    Type = HandleType(sig.ReturnType),
                    Parameters = parameters,
                },
            };

            if (getterHasMetadata)
            {
                result.MethodSemantics.Add(new MethodSemantics
                {
                    Attributes = MethodSemanticsAttributes.Getter,
                    Method = HandleMethodDefinition(getterMethod),
                });
            }

            if (setterHasMetadata)
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
