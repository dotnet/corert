// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    // Extensions to TypeDesc suitable for code generation purposes
    partial class TypeDesc
    {
        /// <summary>
        /// Validates that a type can be fully loaded. Throws an exception if a failure occurs.
        /// </summary>
        public virtual void ValidateCanLoad()
        {
            if (_runtimeInterfaces == null)
            {
                InitializeRuntimeInterfaces();
            }

            DefType baseType = BaseType;
            if (baseType != null)
                baseType.ValidateCanLoad();

            // TODO: more validation (e.g. do interface and virtual methods resolve, etc.)
        }
    }

    // Extensions to DefType suitable for code generation purposes
    partial class DefType : TypeDesc
    {
        public override void ValidateCanLoad()
        {
            base.ValidateCanLoad();

            ComputeInstanceLayout(InstanceLayoutKind.TypeAndFields);
            ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizesAndFields);
        }
    }

    // Extensions to ParameterizedType suitable for code generation purposes
    partial class ParameterizedType : TypeDesc
    {
        public override void ValidateCanLoad()
        {
            base.ValidateCanLoad();

            _parameterType.ValidateCanLoad();
        }
    }
}
