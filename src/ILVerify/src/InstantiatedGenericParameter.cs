// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    internal sealed partial class InstantiatedGenericParameter : GenericParameterDesc
    {
        private readonly GenericParameterDesc _genericParam;
        private readonly Instantiation _typeInstantiation;
        private readonly Instantiation _methodInstantiation;

        public GenericParameterDesc GenericParameter
        {
            get
            {
                return _genericParam;
            }
        }

        public Instantiation TypeInstantiation
        {
            get
            {
                return _typeInstantiation;
            }
        }

        public Instantiation MethodInstantiation
        {
            get
            {
                return _methodInstantiation;
            }
        }

        private Instantiation _typeGenericInstantiation;
        private Instantiation _methodGenericInstantiation;
        private bool instantiationsIntitialized = false;

        internal InstantiatedGenericParameter(GenericParameterDesc genericParam, Instantiation typeInstantiation, Instantiation methodInstantation)
        {
            Debug.Assert(!(genericParam is InstantiatedGenericParameter));
            _genericParam = genericParam;

            Debug.Assert(typeInstantiation.Length > 0 || methodInstantation.Length > 0);
            _typeInstantiation = typeInstantiation;
            _methodInstantiation = methodInstantation;
        }

        private Instantiation SubstituteInstantiation(Instantiation instantiation)
        {
            if (instantiation.Length <= 0)
                return instantiation;

            var parameters = new TypeDesc[instantiation.Length];

            for (int i = 0; i < instantiation.Length; ++i)
            {
                if (instantiation[i].IsGenericParameter)
                {
                    if (instantiation[i] == GenericParameter)
                        parameters[i] = this;
                    else
                        parameters[i] = instantiation[i].Context.GetInstantiatedGenericParameter(
                            (GenericParameterDesc)instantiation[i], _typeInstantiation, _methodInstantiation);
                }
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
                if (!instantiationsIntitialized)
                {
                    _typeGenericInstantiation = SubstituteInstantiation(_typeInstantiation);
                    _methodGenericInstantiation = SubstituteInstantiation(_methodInstantiation);
                    instantiationsIntitialized = true;
                }

                foreach (var constraint in _genericParam.TypeConstraints)
                {
                    yield return constraint.InstantiateSignature(_typeGenericInstantiation, _methodGenericInstantiation);
                }
            }
        }

        public override string ToString()
        {
            return _genericParam.ToString();
        }
    }
}
