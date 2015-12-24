// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    // Abstract base class for the runtime's implementation of System.Type for generic parameters (both type variables and method variables.)
    //
    // - Generic parameters never have EETypes so this is the only class that implements them.
    //
    internal abstract class RuntimeGenericParameterType : RuntimeType
    {
        internal RuntimeGenericParameterType(MetadataReader reader, GenericParameterHandle genericParameterHandle)
        {
            _reader = reader;
            _genericParameterHandle = genericParameterHandle;
            _genericParameter = _genericParameterHandle.GetGenericParameter(_reader);
            _position = _genericParameter.Number;
        }

        public sealed override bool Equals(Object obj)
        {
            return InternalIsEqual(obj);  // Do not change this - see comments in RuntimeType.cs regarding Equals()
        }

        public sealed override int GetHashCode()
        {
            return _genericParameterHandle.GetHashCode();
        }

        public abstract override Type DeclaringType { get; }

        public sealed override String FullName
        {
            get
            {
                return null;
            }
        }

        public sealed override int GenericParameterPosition
        {
            get
            {
                return _position;
            }
        }

        public sealed override bool IsGenericParameter
        {
            get
            {
                return true;
            }
        }

        public sealed override String Namespace
        {
            get
            {
                return DeclaringType.Namespace;
            }
        }

        public sealed override String ToString()
        {
            return Name;
        }

        public sealed override String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure)
        {
            if (_genericParameter.Name.IsNull(_reader))
                return String.Empty;
            return _genericParameter.Name.GetString(_reader);
        }

        public sealed override String InternalFullNameOfAssembly
        {
            get
            {
                Debug.Assert(false, "Why are you bothering to call me when my FullName is null?");
                return null;
            }
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                return true;
            }
        }

        internal GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                return _genericParameter.Flags;
            }
        }

        internal MetadataReader Reader
        {
            get
            {
                return _reader;
            }
        }

        internal GenericParameterHandle GenericParameterHandle
        {
            get
            {
                return _genericParameterHandle;
            }
        }

        internal abstract RuntimeMethodInfo DeclaringMethod { get; }

        internal abstract TypeContext TypeContext { get; }

        internal IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                return RuntimeCustomAttributeData.GetCustomAttributes(this.GetReflectionDomain(), _reader, _genericParameter.CustomAttributes);
            }
        }

        private MetadataReader _reader;
        private GenericParameterHandle _genericParameterHandle;
        private GenericParameter _genericParameter;

        private int _position;
    }
}
