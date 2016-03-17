// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;
using Ecma = System.Reflection.Metadata;

using GenericParameterKind = Internal.Metadata.NativeFormat.GenericParameterKind;

namespace ILCompiler.Metadata
{
    partial class Transform<TPolicy>
    {
        private EntityMap<Cts.TypeDesc, ParameterTypeSignature> _paramSigs =
            new EntityMap<Cts.TypeDesc, ParameterTypeSignature>(EqualityComparer<Cts.TypeDesc>.Default);
        private Action<Cts.TypeDesc, ParameterTypeSignature> _initParamSig;

        private ParameterTypeSignature HandleParameterTypeSignature(Cts.TypeDesc parameter)
        {
            return _paramSigs.GetOrCreate(parameter, _initParamSig ?? (_initParamSig = InitializeParameterTypeSignature));
        }

        private void InitializeParameterTypeSignature(Cts.TypeDesc entity, ParameterTypeSignature record)
        {
            // TODO: CustomModifiers
            record.Type = HandleType(entity);
        }

        #region Generic Parameters

        private GenericParameter HandleGenericParameter(Cts.GenericParameterDesc genParam)
        {
            var result = new GenericParameter
            {
                Kind = genParam.Kind == Cts.GenericParameterKind.Type ?
                    GenericParameterKind.GenericTypeParameter : GenericParameterKind.GenericMethodParameter,
                Number = checked((ushort)genParam.Index),
            };

            foreach (Cts.TypeDesc constraint in genParam.TypeConstraints)
            {
                result.Constraints.Add(HandleType(constraint));
            }

            var ecmaGenParam = genParam as Cts.Ecma.EcmaGenericParameter;
            if (ecmaGenParam != null)
            {
                Ecma.MetadataReader reader = ecmaGenParam.MetadataReader;
                Ecma.GenericParameter genParamDef = reader.GetGenericParameter(ecmaGenParam.Handle);

                result.Flags = genParamDef.Attributes;
                result.Name = HandleString(reader.GetString(genParamDef.Name));

                Ecma.CustomAttributeHandleCollection customAttributes = genParamDef.GetCustomAttributes();
                if (customAttributes.Count > 0)
                {
                    result.CustomAttributes = HandleCustomAttributes(ecmaGenParam.Module, customAttributes);
                }
            }
            else
                throw new NotImplementedException();

            return result;
        }

        #endregion
    }
}
