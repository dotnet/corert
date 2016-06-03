// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    /// <summary>
    /// Represents an unmanaged pointer type.
    /// </summary>
    public sealed partial class PointerType : ParameterizedType
    {
        internal PointerType(TypeDesc parameterType)
            : base(parameterType)
        {
        }

        public override int GetHashCode()
        {
            return Internal.NativeFormat.TypeHashingAlgorithms.ComputePointerTypeHashCode(this.ParameterType.GetHashCode());
        }

        public override TypeDesc InstantiateSignature(Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            TypeDesc instantiatedParameterType = this.ParameterType.InstantiateSignature(typeInstantiation, methodInstantiation);
            return instantiatedParameterType.Context.GetPointerType(instantiatedParameterType);
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = TypeFlags.Pointer;

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
            return this.ParameterType.ToString() + "*";
        }
    }
}
