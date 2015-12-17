// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

using GenericParameterKind = Internal.Metadata.NativeFormat.GenericParameterKind;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
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

            // TODO: need to expose variance/constraints on GenericParameterDesc to make this
            //       useful without a cast. We cannot pretend those are not there.
            throw new System.NotImplementedException();
        }

        #endregion
    }
}
