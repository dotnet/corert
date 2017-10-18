// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    internal sealed partial class InstantiatedGenericParameter : GenericParameterDesc
    {
        private GenericParameterDesc _genericParam;
        public Instantiation TypeInstantiation
        {
            get;
            set;
        }
        public Instantiation MethodInstantiation
        {
            get;
            set;
        }

        internal InstantiatedGenericParameter(GenericParameterDesc genericParam)
        {
            Debug.Assert(!(genericParam is InstantiatedGenericParameter));
            _genericParam = genericParam;
        }

        public override GenericParameterKind Kind => _genericParam.Kind;

        public override int Index => _genericParam.Index;

        public override TypeSystemContext Context => _genericParam.Context;

        public override GenericVariance Variance => _genericParam.Variance;

        public override GenericConstraints Constraints => _genericParam.Constraints;

        public override IEnumerable<TypeDesc> TypeConstraints
        {
            get
            {
                foreach (var constraint in _genericParam.TypeConstraints)
                {
                    yield return constraint.InstantiateSignature(TypeInstantiation, MethodInstantiation);
                }
            }
        }

        public override string ToString()
        {
            return _genericParam.ToString();
        }
    }
}
