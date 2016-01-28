// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.Types
{
    //
    // The runtime's implementation of System.Type for generic parameters for types (not methods.)
    //

    internal sealed class RuntimeGenericParameterTypeForTypes : RuntimeGenericParameterType
    {
        internal RuntimeGenericParameterTypeForTypes(MetadataReader reader, GenericParameterHandle genericParameterHandle, RuntimeNamedTypeInfo declaringRuntimeNamedTypeInfo)
            : base(reader, genericParameterHandle)
        {
            _declaringRuntimeNamedTypeInfo = declaringRuntimeNamedTypeInfo;
        }

        public sealed override Type DeclaringType
        {
            get
            {
                return _declaringRuntimeNamedTypeInfo.AsType();
            }
        }

        internal sealed override RuntimeMethodInfo DeclaringMethod
        {
            get
            {
                return null;
            }
        }

        internal sealed override TypeContext TypeContext
        {
            get
            {
                return new TypeContext(_declaringRuntimeNamedTypeInfo.RuntimeGenericTypeParameters, null);
            }
        }

        private RuntimeNamedTypeInfo _declaringRuntimeNamedTypeInfo;


        //
        // Key for unification.
        //
        internal struct UnificationKey : IEquatable<UnificationKey>
        {
            public UnificationKey(MetadataReader reader, TypeDefinitionHandle typeDefinitionHandle, GenericParameterHandle genericParameterHandle)
            {
                _reader = reader;
                _typeDefinitionHandle = typeDefinitionHandle;
                _genericParameterHandle = genericParameterHandle;
            }

            public MetadataReader Reader
            {
                get
                {
                    return _reader;
                }
            }

            public TypeDefinitionHandle TypeDefinitionHandle
            {
                get
                {
                    return _typeDefinitionHandle;
                }
            }

            public GenericParameterHandle GenericParameterHandle
            {
                get
                {
                    return _genericParameterHandle;
                }
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is UnificationKey))
                    return false;
                return Equals((UnificationKey)obj);
            }

            public bool Equals(UnificationKey other)
            {
                if (!this._typeDefinitionHandle.Equals(other._typeDefinitionHandle))
                    return false;
                if (!(this._reader == other._reader))
                    return false;
                if (!(this._genericParameterHandle.Equals(other._genericParameterHandle)))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                return this._typeDefinitionHandle.GetHashCode();
            }

            private MetadataReader _reader;
            private TypeDefinitionHandle _typeDefinitionHandle;
            private GenericParameterHandle _genericParameterHandle;
        }
    }
}

