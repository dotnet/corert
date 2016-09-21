// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents a managed pointer type.
    /// </summary>
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
            TypeDesc parameterType = this.ParameterType;
            TypeDesc instantiatedParameterType = parameterType.InstantiateSignature(typeInstantiation, methodInstantiation);
            if (instantiatedParameterType != parameterType)
                return Context.GetByRefType(instantiatedParameterType);

            return this;
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
