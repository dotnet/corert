// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Reflection.Runtime.General;

using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // The runtime's implementation of TypeInfo's for the "HasElement" subclass of types. 
    //
    // For now, only Array has its own base class below this - that's the only with that implements anything differently.
    //
    internal partial class RuntimeHasElementTypeInfo : RuntimeTypeInfo
    {
        protected RuntimeHasElementTypeInfo(RuntimeType hasElementType)
            : base()
        {
            Debug.Assert(hasElementType.HasElementType);
            _asType = hasElementType;
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeHasElementTypeInfo other = obj as RuntimeHasElementTypeInfo;
            if (other == null)
                return false;
            return _asType.Equals(other._asType);
        }

        public sealed override int GetHashCode()
        {
            return _asType.GetHashCode();
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return ElementTypeInfo.Assembly;
            }
        }

        //
        // Left unsealed because this implemention is correct for ByRefs and Pointers but not Arrays.
        //
        public override TypeAttributes Attributes
        {
            get
            {
                Debug.Assert(IsByRef || IsPointer);
                return TypeAttributes.AnsiClass;
            }
        }

        internal sealed override RuntimeType RuntimeType
        {
            get
            {
                return _asType;
            }
        }

        private RuntimeType _asType;

        private RuntimeTypeInfo ElementTypeInfo
        {
            get
            {
                if (_elementTypeInfo == null)
                {
                    _elementTypeInfo = _asType.InternalRuntimeElementType.GetRuntimeTypeInfo();
                }
                return _elementTypeInfo;
            }
        }

        private volatile RuntimeTypeInfo _elementTypeInfo;
    }
}

