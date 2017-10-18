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
        private Instantiation _typeInstantiation;
        private Instantiation _methodInstantiation;

        internal InstantiatedGenericParameter(GenericParameterDesc genericParam, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            Debug.Assert(!(genericParam is InstantiatedGenericParameter));
            _genericParam = genericParam;

            Debug.Assert(typeInstantiation.Length > 0 || methodInstantiation.Length > 0);
            _typeInstantiation = SubstituteInstantiation(ref typeInstantiation);
            _methodInstantiation = SubstituteInstantiation(ref methodInstantiation);
        }

        // Substitute _genericParam in given instantiation with this.
        // Required to be able to compare the instantiated constraints of this generic parameter
        // with other instantiated types that were instantiated with this type.
        private Instantiation SubstituteInstantiation(ref Instantiation instantiation)
        {
            var parameters = new TypeDesc[instantiation.Length];

            for (int i = 0; i < instantiation.Length; i++)
            {
                if (instantiation[i] == _genericParam)
                    parameters[i] = this;
                else
                    parameters[i] = instantiation[i];
            }

            return new Instantiation(parameters);
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
                    yield return constraint.InstantiateSignature(_typeInstantiation, _methodInstantiation);
                }
            }
        }

        public override string ToString()
        {
            return _genericParam.ToString();
        }
    }
}
