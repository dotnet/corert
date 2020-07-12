// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// NOTE: This is a generated file - do not manually edit!

using System;
using System.Reflection;
using System.Collections.Generic;

#pragma warning disable 108     // base type 'uint' is not CLS-compliant
#pragma warning disable 3009    // base type 'uint' is not CLS-compliant
#pragma warning disable 282     // There is no defined ordering between fields in multiple declarations of partial class or struct

namespace Internal.Metadata.NativeFormat
{
    [Flags]
    public enum AssemblyFlags : uint
    {
        /// The assembly reference holds the full (unhashed) public key.
        PublicKey = 0x1,

        /// The implementation of this assembly used at runtime is not expected to match the version seen at compile time.
        Retargetable = 0x100,

        /// Reserved.
        DisableJITcompileOptimizer = 0x4000,

        /// Reserved.
        EnableJITcompileTracking = 0x8000,
    } // AssemblyFlags

    public enum AssemblyHashAlgorithm : uint
    {
        None = 0x0,
        Reserved = 0x8003,
        SHA1 = 0x8004,
    } // AssemblyHashAlgorithm

    [Flags]
    public enum FixedArgumentAttributes : byte
    {
        None = 0x0,

        /// Values should be boxed as Object
        Boxed = 0x1,
    } // FixedArgumentAttributes

    public enum GenericParameterKind : byte
    {
        /// Represents a type parameter for a generic type.
        GenericTypeParameter = 0x0,

        /// Represents a type parameter from a generic method.
        GenericMethodParameter = 0x1,
    } // GenericParameterKind

    public enum NamedArgumentMemberKind : byte
    {
        /// Specifies the name of a property
        Property = 0x0,

        /// Specifies the name of a field
        Field = 0x1,
    } // NamedArgumentMemberKind

    public enum HandleType : byte
    {
        Null = 0x0,
        ArraySignature = 0x1,
        ByReferenceSignature = 0x2,
        ConstantBooleanArray = 0x3,
        ConstantBooleanValue = 0x4,
        ConstantBoxedEnumValue = 0x5,
        ConstantByteArray = 0x6,
        ConstantByteValue = 0x7,
        ConstantCharArray = 0x8,
        ConstantCharValue = 0x9,
        ConstantDoubleArray = 0xa,
        ConstantDoubleValue = 0xb,
        ConstantEnumArray = 0xc,
        ConstantHandleArray = 0xd,
        ConstantInt16Array = 0xe,
        ConstantInt16Value = 0xf,
        ConstantInt32Array = 0x10,
        ConstantInt32Value = 0x11,
        ConstantInt64Array = 0x12,
        ConstantInt64Value = 0x13,
        ConstantReferenceValue = 0x14,
        ConstantSByteArray = 0x15,
        ConstantSByteValue = 0x16,
        ConstantSingleArray = 0x17,
        ConstantSingleValue = 0x18,
        ConstantStringArray = 0x19,
        ConstantStringValue = 0x1a,
        ConstantUInt16Array = 0x1b,
        ConstantUInt16Value = 0x1c,
        ConstantUInt32Array = 0x1d,
        ConstantUInt32Value = 0x1e,
        ConstantUInt64Array = 0x1f,
        ConstantUInt64Value = 0x20,
        CustomAttribute = 0x21,
        Event = 0x22,
        Field = 0x23,
        FieldSignature = 0x24,
        FixedArgument = 0x25,
        FunctionPointerSignature = 0x26,
        GenericParameter = 0x27,
        MemberReference = 0x28,
        Method = 0x29,
        MethodImpl = 0x2a,
        MethodInstantiation = 0x2b,
        MethodSemantics = 0x2c,
        MethodSignature = 0x2d,
        MethodTypeVariableSignature = 0x2e,
        ModifiedType = 0x2f,
        NamedArgument = 0x30,
        NamespaceDefinition = 0x31,
        NamespaceReference = 0x32,
        Parameter = 0x33,
        PointerSignature = 0x34,
        Property = 0x35,
        PropertySignature = 0x36,
        QualifiedField = 0x37,
        QualifiedMethod = 0x38,
        SZArraySignature = 0x39,
        ScopeDefinition = 0x3a,
        ScopeReference = 0x3b,
        TypeDefinition = 0x3c,
        TypeForwarder = 0x3d,
        TypeInstantiationSignature = 0x3e,
        TypeReference = 0x3f,
        TypeSpecification = 0x40,
        TypeVariableSignature = 0x41,
    } // HandleType
} // Internal.Metadata.NativeFormat
