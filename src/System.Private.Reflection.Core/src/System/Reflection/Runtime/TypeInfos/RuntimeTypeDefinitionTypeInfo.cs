// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Runtime.TypeInfos
{
    //
    // TypeInfos that represent non-constructed types (IsTypeDefinition == true)
    // 
    internal abstract class RuntimeTypeDefinitionTypeInfo : RuntimeTypeInfo
    {
        public sealed override bool IsTypeDefinition => true;
        public abstract override bool IsGenericTypeDefinition { get; }
        protected sealed override bool HasElementTypeImpl() => false;
        protected sealed override bool IsArrayImpl() => false;
        public sealed override bool IsSZArray => false;
        public sealed override bool IsVariableBoundArray => false;
        protected sealed override bool IsByRefImpl() => false;
        protected sealed override bool IsPointerImpl() => false;
        public sealed override bool IsConstructedGenericType => false;
        public sealed override bool IsGenericParameter => false;
    }
}
