// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Internal.NativeFormat;

using Debug = System.Diagnostics.Debug;
using GenericParameterAttributes = System.Reflection.GenericParameterAttributes;

namespace Internal.TypeSystem.Ecma
{
    public sealed partial class EcmaGenericParameter : GenericParameterDesc
    {
        private EcmaModule _module;
        private GenericParameterHandle _handle;

        internal EcmaGenericParameter(EcmaModule module, GenericParameterHandle handle)
        {
            _module = module;
            _handle = handle;
        }

        public override int GetHashCode()
        {
            // TODO: Determine what a the right hash function should be. Use stable hashcode based on the type name?
            // For now, use the same hash as a SignatureVariable type.
            GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
            return TypeHashingAlgorithms.ComputeSignatureVariableHashCode(parameter.Index, parameter.Parent.Kind == HandleKind.MethodDefinition);
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
                return _module.MetadataReader;
            }
        }

        public EcmaModule Module
        {
            get
            {
                return _module;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _module.Context;
            }
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = 0;

            flags |= TypeFlags.ContainsGenericVariablesComputed | TypeFlags.ContainsGenericVariables;

            flags |= TypeFlags.GenericParameter;

            flags |= TypeFlags.HasGenericVarianceComputed;

            Debug.Assert((flags & mask) != 0);
            return flags;
        }

        public override GenericParameterKind Kind
        {
            get
            {
                GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
                if (parameter.Parent.Kind == HandleKind.MethodDefinition)
                {
                    return GenericParameterKind.Method;
                }
                else
                {
                    Debug.Assert(parameter.Parent.Kind == HandleKind.TypeDefinition);
                    return GenericParameterKind.Type;
                }
            }
        }

        public override int Index
        {
            get
            {
                GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
                return parameter.Index;
            }
        }

        public override GenericVariance Variance
        {
            get
            {
                Debug.Assert((int)GenericVariance.Contravariant == (int)GenericParameterAttributes.Contravariant);
                GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
                return (GenericVariance)(parameter.Attributes & GenericParameterAttributes.VarianceMask);
            }
        }

        public override GenericConstraints Constraints
        {
            get
            {
                Debug.Assert((int)GenericConstraints.DefaultConstructorConstraint == (int)GenericParameterAttributes.DefaultConstructorConstraint);
                GenericParameter parameter = _module.MetadataReader.GetGenericParameter(_handle);
                return (GenericConstraints)(parameter.Attributes & GenericParameterAttributes.SpecialConstraintMask);
            }
        }
        
        public override IEnumerable<TypeDesc> TypeConstraints
        {
            get
            {
                MetadataReader reader = _module.MetadataReader;

                GenericParameter parameter = reader.GetGenericParameter(_handle);
                GenericParameterConstraintHandleCollection constraintHandles = parameter.GetConstraints();

                if (constraintHandles.Count == 0)
                    return Array.Empty<TypeDesc>();

                TypeDesc[] constraintTypes = new TypeDesc[constraintHandles.Count];

                for (int i = 0; i < constraintTypes.Length; i++)
                {
                    GenericParameterConstraint constraint = reader.GetGenericParameterConstraint(constraintHandles[i]);
                    constraintTypes[i] = _module.GetType(constraint.Type);
                };

                return constraintTypes;
            }
        }

        public override string ToString()
        {
            MetadataReader reader = _module.MetadataReader;
            return reader.GetString(reader.GetGenericParameter(_handle).Name);
        }
    }
}
