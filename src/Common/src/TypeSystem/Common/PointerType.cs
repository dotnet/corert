// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    public sealed class PointerType : ParameterizedType
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

        public override TypeDesc GetTypeDefinition()
        {
            TypeDesc result = this;

            TypeDesc parameterDef = this.ParameterType.GetTypeDefinition();
            if (parameterDef != this.ParameterType)
                result = parameterDef.Context.GetPointerType(parameterDef);

            return result;
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

            return flags;
        }

        public override string ToString()
        {
            return this.ParameterType.ToString() + "*";
        }
    }
}
