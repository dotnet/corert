// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    public sealed partial class ByRefType : ParameterizedType
    {
        internal ByRefType(TypeDesc parameter)
            : base(parameter)
        {
        }

        public override int GetHashCode()
        {
            return Internal.NativeFormat.TypeHashingAlgorithms.ComputeByrefTypeHashCode(this.ParameterType.GetHashCode());
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc instantiatedParameterType = this.ParameterType.InstantiateSignature(typeInstantiation, methodInstantiation);
            return instantiatedParameterType.MakeByRefType();
        }

        public override TypeDesc GetTypeDefinition()
        {
            TypeDesc result = this;

            TypeDesc parameterDef = this.ParameterType.GetTypeDefinition();
            if (parameterDef != this.ParameterType)
                result = parameterDef.MakeByRefType();

            return result;
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = TypeFlags.ByRef;

            if ((mask & TypeFlags.ContainsGenericVariablesComputed) != 0)
            {
                flags |= TypeFlags.ContainsGenericVariablesComputed;
                if (this.ParameterType.ContainsGenericVariables)
                    flags |= TypeFlags.ContainsGenericVariables;
            }

            flags |= TypeFlags.HasGenericVarianceComputed;

            return flags;
        }

        public override string ToString()
        {
            return this.ParameterType.ToString() + "&";
        }
    }
}
