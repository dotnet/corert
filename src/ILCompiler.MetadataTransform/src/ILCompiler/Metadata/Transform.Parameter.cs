// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

using GenericParameterKind = Internal.Metadata.NativeFormat.GenericParameterKind;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {

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
