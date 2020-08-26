// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

#pragma warning disable 169 // Field 'x' is never used

namespace System.Runtime.CompilerServices
{
    [__BlockReflection]
    public class __BlockReflectionAttribute : Attribute
    {
    }
}

namespace BlockedMetadata
{
    [__BlockReflection]
    public class BlockedType
    {
    }

    public class AllowedType
    {
    }

    [__BlockReflection]
    public class BlockedGenericType<T>
    {
    }

    public class AllowedGenericType<T>
    {
    }

    [__BlockReflection]
    public enum BlockedEnum
    {
        One,
        Two,
    }

    public enum AllowedEnum
    {
        One,
        Two,
    }

    public class AttributeHolder
    {
        [My(typeof(AllowedType))]
        int AllowedNongeneric;

        [My(typeof(BlockedType))]
        int BlockedNongeneric;

        [My(typeof(AllowedGenericType<AllowedType>))]
        int AllowedGeneric;

        [My(typeof(BlockedGenericType<AllowedType>))]
        int BlockedGeneric;

        [My(typeof(AllowedGenericType<BlockedType>))]
        int BlockedGenericInstantiation;

        [My(typeof(AllowedGenericType<BlockedType[]>))]
        int BlockedArrayGenericInstantiation;

        [My(AllowedEnum.One)]
        int AllowedEnumType;

        [My(BlockedEnum.One)]
        int BlockedEnumType;

        [Blocked]
        int BlockedAttribute;

        [My(new object[] { typeof(AllowedType) })]
        int AllowedTypeArray;

        [My(new object[] { typeof(BlockedType)})]
        int BlockedTypeArray;

        [My(new object[] { AllowedEnum.One })]
        int AllowedEnumArray;

        [My(new object[] { BlockedEnum.One})]
        int BlockedEnumArray;
    }

    public class MyAttribute : Attribute
    {
        public MyAttribute(Type type)
        {
        }

        public MyAttribute(object[] o)
        {
        }

        public MyAttribute(object o)
        {
        }
    }

    [__BlockReflection]
    public class BlockedAttribute : Attribute
    {
    }
}
