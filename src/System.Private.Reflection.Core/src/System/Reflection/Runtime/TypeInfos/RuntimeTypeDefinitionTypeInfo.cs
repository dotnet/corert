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

        // Left unsealed as RuntimeCLSIDTypeInfo has special behavior and needs to override.
        public override bool HasSameMetadataDefinitionAs(MemberInfo other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            // Do not rewrite as a call to IsConstructedGenericType - we haven't yet established that "other" is a runtime-implemented member yet!
            RuntimeConstructedGenericTypeInfo otherConstructedGenericType = other as RuntimeConstructedGenericTypeInfo;
            if (otherConstructedGenericType != null)
                other = otherConstructedGenericType.GetGenericTypeDefinition();

            // Unlike most other MemberInfo objects, types never get cloned due to containing generic types being instantiated.
            // That is, their DeclaringType is always the generic type definition. As a Type, the ReflectedType property is always equal to the DeclaringType.
            //
            // Because of these conditions, we can safely implement both the method token equivalence and the "is this type from the same implementor"
            // check as our regular Equals() method.
            return Equals(other);
        }
    }
}
