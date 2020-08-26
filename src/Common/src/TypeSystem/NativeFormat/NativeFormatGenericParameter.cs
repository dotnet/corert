// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Threading;

using Internal.NativeFormat;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using GenericParameterAttributes = System.Reflection.GenericParameterAttributes;

namespace Internal.TypeSystem.NativeFormat
{
    public sealed partial class NativeFormatGenericParameter : GenericParameterDesc
    {
        private NativeFormatMetadataUnit _metadataUnit;
        private GenericParameterHandle _handle;

        internal NativeFormatGenericParameter(NativeFormatMetadataUnit metadataUnit, GenericParameterHandle handle)
        {
            _metadataUnit = metadataUnit;
            _handle = handle;
        }

        public GenericParameterHandle Handle
        {
            get
            {
                return _handle;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _metadataUnit.MetadataReader;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _metadataUnit.Context;
            }
        }

        public override System.Int32 Index
        {
            get
            {
                GenericParameter parameter = MetadataReader.GetGenericParameter(_handle);
                return parameter.Number;
            }
        }

        public override Internal.TypeSystem.GenericParameterKind Kind
        {
            get
            {
                GenericParameter parameter = MetadataReader.GetGenericParameter(_handle);
                if (parameter.Kind == Internal.Metadata.NativeFormat.GenericParameterKind.GenericMethodParameter)
                {
                    return Internal.TypeSystem.GenericParameterKind.Method;
                }
                else
                {
                    return Internal.TypeSystem.GenericParameterKind.Type;
                }
            }
        }

        public override GenericVariance Variance
        {
            get
            {
                Debug.Assert((int)GenericVariance.Contravariant == (int)GenericParameterAttributes.Contravariant);
                GenericParameter parameter = MetadataReader.GetGenericParameter(_handle);
                return (GenericVariance)(parameter.Flags & GenericParameterAttributes.VarianceMask);
            }
        }

        public override GenericConstraints Constraints
        {
            get
            {
                Debug.Assert((int)GenericConstraints.DefaultConstructorConstraint == (int)GenericParameterAttributes.DefaultConstructorConstraint);
                GenericParameter parameter = MetadataReader.GetGenericParameter(_handle);
                return (GenericConstraints)(parameter.Flags & GenericParameterAttributes.SpecialConstraintMask);
            }
        }

        public override IEnumerable<TypeDesc> TypeConstraints
        {
            get
            {
                MetadataReader reader = MetadataReader;

                GenericParameter parameter = reader.GetGenericParameter(_handle);
                var constraintHandles = parameter.Constraints;

                if (constraintHandles.Count == 0)
                    return TypeDesc.EmptyTypes;

                TypeDesc[] constraintTypes = new TypeDesc[constraintHandles.Count];

                int i = 0;
                foreach (Handle handle in constraintHandles)
                {
                    constraintTypes[i] = _metadataUnit.GetType(handle);
                    i++;
                }

                return constraintTypes;
            }
        }
    }
}
