// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// NOTE: This is a generated file - do not manually edit!

using System;
using System.Reflection;
using System.Collections.Generic;

#pragma warning disable 108     // base type 'uint' is not CLS-compliant
#pragma warning disable 3009    // base type 'uint' is not CLS-compliant
#pragma warning disable 282     // There is no defined ordering between fields in multiple declarations of partial class or struct

namespace Internal.Metadata.NativeFormat
{
    /// <summary>
    /// AssemblyFlags
    /// </summary>
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

    /// <summary>
    /// AssemblyHashAlgorithm
    /// </summary>
    public enum AssemblyHashAlgorithm : uint
    {
        None = 0x0,
        Reserved = 0x8003,
        SHA1 = 0x8004,
    } // AssemblyHashAlgorithm

    /// <summary>
    /// FixedArgumentAttributes
    /// </summary>
    [Flags]
    public enum FixedArgumentAttributes : byte
    {
        None = 0x0,

        /// Values should be boxed as Object
        Boxed = 0x1,
    } // FixedArgumentAttributes

    /// <summary>
    /// GenericParameterKind
    /// </summary>
    public enum GenericParameterKind : byte
    {
        /// Represents a type parameter for a generic type.
        GenericTypeParameter = 0x0,

        /// Represents a type parameter from a generic method.
        GenericMethodParameter = 0x1,
    } // GenericParameterKind

    /// <summary>
    /// HandleType
    /// </summary>
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
        ConstantHandleArray = 0xc,
        ConstantInt16Array = 0xd,
        ConstantInt16Value = 0xe,
        ConstantInt32Array = 0xf,
        ConstantInt32Value = 0x10,
        ConstantInt64Array = 0x11,
        ConstantInt64Value = 0x12,
        ConstantReferenceValue = 0x13,
        ConstantSByteArray = 0x14,
        ConstantSByteValue = 0x15,
        ConstantSingleArray = 0x16,
        ConstantSingleValue = 0x17,
        ConstantStringArray = 0x18,
        ConstantStringValue = 0x19,
        ConstantUInt16Array = 0x1a,
        ConstantUInt16Value = 0x1b,
        ConstantUInt32Array = 0x1c,
        ConstantUInt32Value = 0x1d,
        ConstantUInt64Array = 0x1e,
        ConstantUInt64Value = 0x1f,
        CustomAttribute = 0x20,
        CustomModifier = 0x21,
        Event = 0x22,
        Field = 0x23,
        FieldSignature = 0x24,
        FixedArgument = 0x25,
        GenericParameter = 0x26,
        MemberReference = 0x27,
        Method = 0x28,
        MethodImpl = 0x29,
        MethodInstantiation = 0x2a,
        MethodSemantics = 0x2b,
        MethodSignature = 0x2c,
        MethodTypeVariableSignature = 0x2d,
        NamedArgument = 0x2e,
        NamespaceDefinition = 0x2f,
        NamespaceReference = 0x30,
        Parameter = 0x31,
        ParameterTypeSignature = 0x32,
        PointerSignature = 0x33,
        Property = 0x34,
        PropertySignature = 0x35,
        ReturnTypeSignature = 0x36,
        SZArraySignature = 0x37,
        ScopeDefinition = 0x38,
        ScopeReference = 0x39,
        TypeDefinition = 0x3a,
        TypeForwarder = 0x3b,
        TypeInstantiationSignature = 0x3c,
        TypeReference = 0x3d,
        TypeSpecification = 0x3e,
        TypeVariableSignature = 0x3f,
    } // HandleType

    /// <summary>
    /// NamedArgumentMemberKind
    /// </summary>
    public enum NamedArgumentMemberKind : byte
    {
        /// Specifies the name of a property
        Property = 0x0,

        /// Specifies the name of a field
        Field = 0x1,
    } // NamedArgumentMemberKind

    /// <summary>
    /// IArraySignature
    /// </summary>
    internal interface IArraySignature
    {
        Handle ElementType
        {
            get;
        } // ElementType

        int Rank
        {
            get;
        } // Rank

        IEnumerable<int> Sizes
        {
            get;
        } // Sizes

        IEnumerable<int> LowerBounds
        {
            get;
        } // LowerBounds

        ArraySignatureHandle Handle
        {
            get;
        } // Handle
    } // IArraySignature

    /// <summary>
    /// ArraySignature
    /// </summary>
    public partial struct ArraySignature : IArraySignature
    {
    } // ArraySignature

    /// <summary>
    /// IArraySignatureHandle
    /// </summary>
    internal interface IArraySignatureHandle : IEquatable<ArraySignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IArraySignatureHandle

    /// <summary>
    /// ArraySignatureHandle
    /// </summary>
    public partial struct ArraySignatureHandle : IArraySignatureHandle
    {
    } // ArraySignatureHandle

    /// <summary>
    /// IByReferenceSignature
    /// </summary>
    internal interface IByReferenceSignature
    {
        Handle Type
        {
            get;
        } // Type

        ByReferenceSignatureHandle Handle
        {
            get;
        } // Handle
    } // IByReferenceSignature

    /// <summary>
    /// ByReferenceSignature
    /// </summary>
    public partial struct ByReferenceSignature : IByReferenceSignature
    {
    } // ByReferenceSignature

    /// <summary>
    /// IByReferenceSignatureHandle
    /// </summary>
    internal interface IByReferenceSignatureHandle : IEquatable<ByReferenceSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IByReferenceSignatureHandle

    /// <summary>
    /// ByReferenceSignatureHandle
    /// </summary>
    public partial struct ByReferenceSignatureHandle : IByReferenceSignatureHandle
    {
    } // ByReferenceSignatureHandle

    /// <summary>
    /// IConstantBooleanArray
    /// </summary>
    internal interface IConstantBooleanArray
    {
        IEnumerable<bool> Value
        {
            get;
        } // Value

        ConstantBooleanArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantBooleanArray

    /// <summary>
    /// ConstantBooleanArray
    /// </summary>
    public partial struct ConstantBooleanArray : IConstantBooleanArray
    {
    } // ConstantBooleanArray

    /// <summary>
    /// IConstantBooleanArrayHandle
    /// </summary>
    internal interface IConstantBooleanArrayHandle : IEquatable<ConstantBooleanArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantBooleanArrayHandle

    /// <summary>
    /// ConstantBooleanArrayHandle
    /// </summary>
    public partial struct ConstantBooleanArrayHandle : IConstantBooleanArrayHandle
    {
    } // ConstantBooleanArrayHandle

    /// <summary>
    /// IConstantBooleanValue
    /// </summary>
    internal interface IConstantBooleanValue
    {
        bool Value
        {
            get;
        } // Value

        ConstantBooleanValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantBooleanValue

    /// <summary>
    /// ConstantBooleanValue
    /// </summary>
    public partial struct ConstantBooleanValue : IConstantBooleanValue
    {
    } // ConstantBooleanValue

    /// <summary>
    /// IConstantBooleanValueHandle
    /// </summary>
    internal interface IConstantBooleanValueHandle : IEquatable<ConstantBooleanValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantBooleanValueHandle

    /// <summary>
    /// ConstantBooleanValueHandle
    /// </summary>
    public partial struct ConstantBooleanValueHandle : IConstantBooleanValueHandle
    {
    } // ConstantBooleanValueHandle

    /// <summary>
    /// IConstantBoxedEnumValue
    /// </summary>
    internal interface IConstantBoxedEnumValue
    {
        Handle Value
        {
            get;
        } // Value

        Handle Type
        {
            get;
        } // Type

        ConstantBoxedEnumValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantBoxedEnumValue

    /// <summary>
    /// ConstantBoxedEnumValue
    /// </summary>
    public partial struct ConstantBoxedEnumValue : IConstantBoxedEnumValue
    {
    } // ConstantBoxedEnumValue

    /// <summary>
    /// IConstantBoxedEnumValueHandle
    /// </summary>
    internal interface IConstantBoxedEnumValueHandle : IEquatable<ConstantBoxedEnumValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantBoxedEnumValueHandle

    /// <summary>
    /// ConstantBoxedEnumValueHandle
    /// </summary>
    public partial struct ConstantBoxedEnumValueHandle : IConstantBoxedEnumValueHandle
    {
    } // ConstantBoxedEnumValueHandle

    /// <summary>
    /// IConstantByteArray
    /// </summary>
    internal interface IConstantByteArray
    {
        IEnumerable<byte> Value
        {
            get;
        } // Value

        ConstantByteArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantByteArray

    /// <summary>
    /// ConstantByteArray
    /// </summary>
    public partial struct ConstantByteArray : IConstantByteArray
    {
    } // ConstantByteArray

    /// <summary>
    /// IConstantByteArrayHandle
    /// </summary>
    internal interface IConstantByteArrayHandle : IEquatable<ConstantByteArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantByteArrayHandle

    /// <summary>
    /// ConstantByteArrayHandle
    /// </summary>
    public partial struct ConstantByteArrayHandle : IConstantByteArrayHandle
    {
    } // ConstantByteArrayHandle

    /// <summary>
    /// IConstantByteValue
    /// </summary>
    internal interface IConstantByteValue
    {
        byte Value
        {
            get;
        } // Value

        ConstantByteValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantByteValue

    /// <summary>
    /// ConstantByteValue
    /// </summary>
    public partial struct ConstantByteValue : IConstantByteValue
    {
    } // ConstantByteValue

    /// <summary>
    /// IConstantByteValueHandle
    /// </summary>
    internal interface IConstantByteValueHandle : IEquatable<ConstantByteValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantByteValueHandle

    /// <summary>
    /// ConstantByteValueHandle
    /// </summary>
    public partial struct ConstantByteValueHandle : IConstantByteValueHandle
    {
    } // ConstantByteValueHandle

    /// <summary>
    /// IConstantCharArray
    /// </summary>
    internal interface IConstantCharArray
    {
        IEnumerable<char> Value
        {
            get;
        } // Value

        ConstantCharArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantCharArray

    /// <summary>
    /// ConstantCharArray
    /// </summary>
    public partial struct ConstantCharArray : IConstantCharArray
    {
    } // ConstantCharArray

    /// <summary>
    /// IConstantCharArrayHandle
    /// </summary>
    internal interface IConstantCharArrayHandle : IEquatable<ConstantCharArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantCharArrayHandle

    /// <summary>
    /// ConstantCharArrayHandle
    /// </summary>
    public partial struct ConstantCharArrayHandle : IConstantCharArrayHandle
    {
    } // ConstantCharArrayHandle

    /// <summary>
    /// IConstantCharValue
    /// </summary>
    internal interface IConstantCharValue
    {
        char Value
        {
            get;
        } // Value

        ConstantCharValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantCharValue

    /// <summary>
    /// ConstantCharValue
    /// </summary>
    public partial struct ConstantCharValue : IConstantCharValue
    {
    } // ConstantCharValue

    /// <summary>
    /// IConstantCharValueHandle
    /// </summary>
    internal interface IConstantCharValueHandle : IEquatable<ConstantCharValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantCharValueHandle

    /// <summary>
    /// ConstantCharValueHandle
    /// </summary>
    public partial struct ConstantCharValueHandle : IConstantCharValueHandle
    {
    } // ConstantCharValueHandle

    /// <summary>
    /// IConstantDoubleArray
    /// </summary>
    internal interface IConstantDoubleArray
    {
        IEnumerable<double> Value
        {
            get;
        } // Value

        ConstantDoubleArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantDoubleArray

    /// <summary>
    /// ConstantDoubleArray
    /// </summary>
    public partial struct ConstantDoubleArray : IConstantDoubleArray
    {
    } // ConstantDoubleArray

    /// <summary>
    /// IConstantDoubleArrayHandle
    /// </summary>
    internal interface IConstantDoubleArrayHandle : IEquatable<ConstantDoubleArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantDoubleArrayHandle

    /// <summary>
    /// ConstantDoubleArrayHandle
    /// </summary>
    public partial struct ConstantDoubleArrayHandle : IConstantDoubleArrayHandle
    {
    } // ConstantDoubleArrayHandle

    /// <summary>
    /// IConstantDoubleValue
    /// </summary>
    internal interface IConstantDoubleValue
    {
        double Value
        {
            get;
        } // Value

        ConstantDoubleValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantDoubleValue

    /// <summary>
    /// ConstantDoubleValue
    /// </summary>
    public partial struct ConstantDoubleValue : IConstantDoubleValue
    {
    } // ConstantDoubleValue

    /// <summary>
    /// IConstantDoubleValueHandle
    /// </summary>
    internal interface IConstantDoubleValueHandle : IEquatable<ConstantDoubleValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantDoubleValueHandle

    /// <summary>
    /// ConstantDoubleValueHandle
    /// </summary>
    public partial struct ConstantDoubleValueHandle : IConstantDoubleValueHandle
    {
    } // ConstantDoubleValueHandle

    /// <summary>
    /// IConstantHandleArray
    /// </summary>
    internal interface IConstantHandleArray
    {
        IEnumerable<Handle> Value
        {
            get;
        } // Value

        ConstantHandleArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantHandleArray

    /// <summary>
    /// ConstantHandleArray
    /// </summary>
    public partial struct ConstantHandleArray : IConstantHandleArray
    {
    } // ConstantHandleArray

    /// <summary>
    /// IConstantHandleArrayHandle
    /// </summary>
    internal interface IConstantHandleArrayHandle : IEquatable<ConstantHandleArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantHandleArrayHandle

    /// <summary>
    /// ConstantHandleArrayHandle
    /// </summary>
    public partial struct ConstantHandleArrayHandle : IConstantHandleArrayHandle
    {
    } // ConstantHandleArrayHandle

    /// <summary>
    /// IConstantInt16Array
    /// </summary>
    internal interface IConstantInt16Array
    {
        IEnumerable<short> Value
        {
            get;
        } // Value

        ConstantInt16ArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantInt16Array

    /// <summary>
    /// ConstantInt16Array
    /// </summary>
    public partial struct ConstantInt16Array : IConstantInt16Array
    {
    } // ConstantInt16Array

    /// <summary>
    /// IConstantInt16ArrayHandle
    /// </summary>
    internal interface IConstantInt16ArrayHandle : IEquatable<ConstantInt16ArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantInt16ArrayHandle

    /// <summary>
    /// ConstantInt16ArrayHandle
    /// </summary>
    public partial struct ConstantInt16ArrayHandle : IConstantInt16ArrayHandle
    {
    } // ConstantInt16ArrayHandle

    /// <summary>
    /// IConstantInt16Value
    /// </summary>
    internal interface IConstantInt16Value
    {
        short Value
        {
            get;
        } // Value

        ConstantInt16ValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantInt16Value

    /// <summary>
    /// ConstantInt16Value
    /// </summary>
    public partial struct ConstantInt16Value : IConstantInt16Value
    {
    } // ConstantInt16Value

    /// <summary>
    /// IConstantInt16ValueHandle
    /// </summary>
    internal interface IConstantInt16ValueHandle : IEquatable<ConstantInt16ValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantInt16ValueHandle

    /// <summary>
    /// ConstantInt16ValueHandle
    /// </summary>
    public partial struct ConstantInt16ValueHandle : IConstantInt16ValueHandle
    {
    } // ConstantInt16ValueHandle

    /// <summary>
    /// IConstantInt32Array
    /// </summary>
    internal interface IConstantInt32Array
    {
        IEnumerable<int> Value
        {
            get;
        } // Value

        ConstantInt32ArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantInt32Array

    /// <summary>
    /// ConstantInt32Array
    /// </summary>
    public partial struct ConstantInt32Array : IConstantInt32Array
    {
    } // ConstantInt32Array

    /// <summary>
    /// IConstantInt32ArrayHandle
    /// </summary>
    internal interface IConstantInt32ArrayHandle : IEquatable<ConstantInt32ArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantInt32ArrayHandle

    /// <summary>
    /// ConstantInt32ArrayHandle
    /// </summary>
    public partial struct ConstantInt32ArrayHandle : IConstantInt32ArrayHandle
    {
    } // ConstantInt32ArrayHandle

    /// <summary>
    /// IConstantInt32Value
    /// </summary>
    internal interface IConstantInt32Value
    {
        int Value
        {
            get;
        } // Value

        ConstantInt32ValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantInt32Value

    /// <summary>
    /// ConstantInt32Value
    /// </summary>
    public partial struct ConstantInt32Value : IConstantInt32Value
    {
    } // ConstantInt32Value

    /// <summary>
    /// IConstantInt32ValueHandle
    /// </summary>
    internal interface IConstantInt32ValueHandle : IEquatable<ConstantInt32ValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantInt32ValueHandle

    /// <summary>
    /// ConstantInt32ValueHandle
    /// </summary>
    public partial struct ConstantInt32ValueHandle : IConstantInt32ValueHandle
    {
    } // ConstantInt32ValueHandle

    /// <summary>
    /// IConstantInt64Array
    /// </summary>
    internal interface IConstantInt64Array
    {
        IEnumerable<long> Value
        {
            get;
        } // Value

        ConstantInt64ArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantInt64Array

    /// <summary>
    /// ConstantInt64Array
    /// </summary>
    public partial struct ConstantInt64Array : IConstantInt64Array
    {
    } // ConstantInt64Array

    /// <summary>
    /// IConstantInt64ArrayHandle
    /// </summary>
    internal interface IConstantInt64ArrayHandle : IEquatable<ConstantInt64ArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantInt64ArrayHandle

    /// <summary>
    /// ConstantInt64ArrayHandle
    /// </summary>
    public partial struct ConstantInt64ArrayHandle : IConstantInt64ArrayHandle
    {
    } // ConstantInt64ArrayHandle

    /// <summary>
    /// IConstantInt64Value
    /// </summary>
    internal interface IConstantInt64Value
    {
        long Value
        {
            get;
        } // Value

        ConstantInt64ValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantInt64Value

    /// <summary>
    /// ConstantInt64Value
    /// </summary>
    public partial struct ConstantInt64Value : IConstantInt64Value
    {
    } // ConstantInt64Value

    /// <summary>
    /// IConstantInt64ValueHandle
    /// </summary>
    internal interface IConstantInt64ValueHandle : IEquatable<ConstantInt64ValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantInt64ValueHandle

    /// <summary>
    /// ConstantInt64ValueHandle
    /// </summary>
    public partial struct ConstantInt64ValueHandle : IConstantInt64ValueHandle
    {
    } // ConstantInt64ValueHandle

    /// <summary>
    /// IConstantReferenceValue
    /// </summary>
    internal interface IConstantReferenceValue
    {
        Object Value
        {
            get;
        } // Value

        ConstantReferenceValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantReferenceValue

    /// <summary>
    /// ConstantReferenceValue
    /// </summary>
    public partial struct ConstantReferenceValue : IConstantReferenceValue
    {
    } // ConstantReferenceValue

    /// <summary>
    /// IConstantReferenceValueHandle
    /// </summary>
    internal interface IConstantReferenceValueHandle : IEquatable<ConstantReferenceValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantReferenceValueHandle

    /// <summary>
    /// ConstantReferenceValueHandle
    /// </summary>
    public partial struct ConstantReferenceValueHandle : IConstantReferenceValueHandle
    {
    } // ConstantReferenceValueHandle

    /// <summary>
    /// IConstantSByteArray
    /// </summary>
    internal interface IConstantSByteArray
    {
        IEnumerable<sbyte> Value
        {
            get;
        } // Value

        ConstantSByteArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantSByteArray

    /// <summary>
    /// ConstantSByteArray
    /// </summary>
    public partial struct ConstantSByteArray : IConstantSByteArray
    {
    } // ConstantSByteArray

    /// <summary>
    /// IConstantSByteArrayHandle
    /// </summary>
    internal interface IConstantSByteArrayHandle : IEquatable<ConstantSByteArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantSByteArrayHandle

    /// <summary>
    /// ConstantSByteArrayHandle
    /// </summary>
    public partial struct ConstantSByteArrayHandle : IConstantSByteArrayHandle
    {
    } // ConstantSByteArrayHandle

    /// <summary>
    /// IConstantSByteValue
    /// </summary>
    internal interface IConstantSByteValue
    {
        sbyte Value
        {
            get;
        } // Value

        ConstantSByteValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantSByteValue

    /// <summary>
    /// ConstantSByteValue
    /// </summary>
    public partial struct ConstantSByteValue : IConstantSByteValue
    {
    } // ConstantSByteValue

    /// <summary>
    /// IConstantSByteValueHandle
    /// </summary>
    internal interface IConstantSByteValueHandle : IEquatable<ConstantSByteValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantSByteValueHandle

    /// <summary>
    /// ConstantSByteValueHandle
    /// </summary>
    public partial struct ConstantSByteValueHandle : IConstantSByteValueHandle
    {
    } // ConstantSByteValueHandle

    /// <summary>
    /// IConstantSingleArray
    /// </summary>
    internal interface IConstantSingleArray
    {
        IEnumerable<float> Value
        {
            get;
        } // Value

        ConstantSingleArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantSingleArray

    /// <summary>
    /// ConstantSingleArray
    /// </summary>
    public partial struct ConstantSingleArray : IConstantSingleArray
    {
    } // ConstantSingleArray

    /// <summary>
    /// IConstantSingleArrayHandle
    /// </summary>
    internal interface IConstantSingleArrayHandle : IEquatable<ConstantSingleArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantSingleArrayHandle

    /// <summary>
    /// ConstantSingleArrayHandle
    /// </summary>
    public partial struct ConstantSingleArrayHandle : IConstantSingleArrayHandle
    {
    } // ConstantSingleArrayHandle

    /// <summary>
    /// IConstantSingleValue
    /// </summary>
    internal interface IConstantSingleValue
    {
        float Value
        {
            get;
        } // Value

        ConstantSingleValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantSingleValue

    /// <summary>
    /// ConstantSingleValue
    /// </summary>
    public partial struct ConstantSingleValue : IConstantSingleValue
    {
    } // ConstantSingleValue

    /// <summary>
    /// IConstantSingleValueHandle
    /// </summary>
    internal interface IConstantSingleValueHandle : IEquatable<ConstantSingleValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantSingleValueHandle

    /// <summary>
    /// ConstantSingleValueHandle
    /// </summary>
    public partial struct ConstantSingleValueHandle : IConstantSingleValueHandle
    {
    } // ConstantSingleValueHandle

    /// <summary>
    /// IConstantStringArray
    /// </summary>
    internal interface IConstantStringArray
    {
        IEnumerable<string> Value
        {
            get;
        } // Value

        ConstantStringArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantStringArray

    /// <summary>
    /// ConstantStringArray
    /// </summary>
    public partial struct ConstantStringArray : IConstantStringArray
    {
    } // ConstantStringArray

    /// <summary>
    /// IConstantStringArrayHandle
    /// </summary>
    internal interface IConstantStringArrayHandle : IEquatable<ConstantStringArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantStringArrayHandle

    /// <summary>
    /// ConstantStringArrayHandle
    /// </summary>
    public partial struct ConstantStringArrayHandle : IConstantStringArrayHandle
    {
    } // ConstantStringArrayHandle

    /// <summary>
    /// IConstantStringValue
    /// </summary>
    internal interface IConstantStringValue
    {
        string Value
        {
            get;
        } // Value

        ConstantStringValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantStringValue

    /// <summary>
    /// ConstantStringValue
    /// </summary>
    public partial struct ConstantStringValue : IConstantStringValue
    {
    } // ConstantStringValue

    /// <summary>
    /// IConstantStringValueHandle
    /// </summary>
    internal interface IConstantStringValueHandle : IEquatable<ConstantStringValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantStringValueHandle

    /// <summary>
    /// ConstantStringValueHandle
    /// </summary>
    public partial struct ConstantStringValueHandle : IConstantStringValueHandle
    {
    } // ConstantStringValueHandle

    /// <summary>
    /// IConstantUInt16Array
    /// </summary>
    internal interface IConstantUInt16Array
    {
        IEnumerable<ushort> Value
        {
            get;
        } // Value

        ConstantUInt16ArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantUInt16Array

    /// <summary>
    /// ConstantUInt16Array
    /// </summary>
    public partial struct ConstantUInt16Array : IConstantUInt16Array
    {
    } // ConstantUInt16Array

    /// <summary>
    /// IConstantUInt16ArrayHandle
    /// </summary>
    internal interface IConstantUInt16ArrayHandle : IEquatable<ConstantUInt16ArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantUInt16ArrayHandle

    /// <summary>
    /// ConstantUInt16ArrayHandle
    /// </summary>
    public partial struct ConstantUInt16ArrayHandle : IConstantUInt16ArrayHandle
    {
    } // ConstantUInt16ArrayHandle

    /// <summary>
    /// IConstantUInt16Value
    /// </summary>
    internal interface IConstantUInt16Value
    {
        ushort Value
        {
            get;
        } // Value

        ConstantUInt16ValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantUInt16Value

    /// <summary>
    /// ConstantUInt16Value
    /// </summary>
    public partial struct ConstantUInt16Value : IConstantUInt16Value
    {
    } // ConstantUInt16Value

    /// <summary>
    /// IConstantUInt16ValueHandle
    /// </summary>
    internal interface IConstantUInt16ValueHandle : IEquatable<ConstantUInt16ValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantUInt16ValueHandle

    /// <summary>
    /// ConstantUInt16ValueHandle
    /// </summary>
    public partial struct ConstantUInt16ValueHandle : IConstantUInt16ValueHandle
    {
    } // ConstantUInt16ValueHandle

    /// <summary>
    /// IConstantUInt32Array
    /// </summary>
    internal interface IConstantUInt32Array
    {
        IEnumerable<uint> Value
        {
            get;
        } // Value

        ConstantUInt32ArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantUInt32Array

    /// <summary>
    /// ConstantUInt32Array
    /// </summary>
    public partial struct ConstantUInt32Array : IConstantUInt32Array
    {
    } // ConstantUInt32Array

    /// <summary>
    /// IConstantUInt32ArrayHandle
    /// </summary>
    internal interface IConstantUInt32ArrayHandle : IEquatable<ConstantUInt32ArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantUInt32ArrayHandle

    /// <summary>
    /// ConstantUInt32ArrayHandle
    /// </summary>
    public partial struct ConstantUInt32ArrayHandle : IConstantUInt32ArrayHandle
    {
    } // ConstantUInt32ArrayHandle

    /// <summary>
    /// IConstantUInt32Value
    /// </summary>
    internal interface IConstantUInt32Value
    {
        uint Value
        {
            get;
        } // Value

        ConstantUInt32ValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantUInt32Value

    /// <summary>
    /// ConstantUInt32Value
    /// </summary>
    public partial struct ConstantUInt32Value : IConstantUInt32Value
    {
    } // ConstantUInt32Value

    /// <summary>
    /// IConstantUInt32ValueHandle
    /// </summary>
    internal interface IConstantUInt32ValueHandle : IEquatable<ConstantUInt32ValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantUInt32ValueHandle

    /// <summary>
    /// ConstantUInt32ValueHandle
    /// </summary>
    public partial struct ConstantUInt32ValueHandle : IConstantUInt32ValueHandle
    {
    } // ConstantUInt32ValueHandle

    /// <summary>
    /// IConstantUInt64Array
    /// </summary>
    internal interface IConstantUInt64Array
    {
        IEnumerable<ulong> Value
        {
            get;
        } // Value

        ConstantUInt64ArrayHandle Handle
        {
            get;
        } // Handle
    } // IConstantUInt64Array

    /// <summary>
    /// ConstantUInt64Array
    /// </summary>
    public partial struct ConstantUInt64Array : IConstantUInt64Array
    {
    } // ConstantUInt64Array

    /// <summary>
    /// IConstantUInt64ArrayHandle
    /// </summary>
    internal interface IConstantUInt64ArrayHandle : IEquatable<ConstantUInt64ArrayHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantUInt64ArrayHandle

    /// <summary>
    /// ConstantUInt64ArrayHandle
    /// </summary>
    public partial struct ConstantUInt64ArrayHandle : IConstantUInt64ArrayHandle
    {
    } // ConstantUInt64ArrayHandle

    /// <summary>
    /// IConstantUInt64Value
    /// </summary>
    internal interface IConstantUInt64Value
    {
        ulong Value
        {
            get;
        } // Value

        ConstantUInt64ValueHandle Handle
        {
            get;
        } // Handle
    } // IConstantUInt64Value

    /// <summary>
    /// ConstantUInt64Value
    /// </summary>
    public partial struct ConstantUInt64Value : IConstantUInt64Value
    {
    } // ConstantUInt64Value

    /// <summary>
    /// IConstantUInt64ValueHandle
    /// </summary>
    internal interface IConstantUInt64ValueHandle : IEquatable<ConstantUInt64ValueHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IConstantUInt64ValueHandle

    /// <summary>
    /// ConstantUInt64ValueHandle
    /// </summary>
    public partial struct ConstantUInt64ValueHandle : IConstantUInt64ValueHandle
    {
    } // ConstantUInt64ValueHandle

    /// <summary>
    /// ICustomAttribute
    /// </summary>
    internal interface ICustomAttribute
    {
        Handle Type
        {
            get;
        } // Type

        Handle Constructor
        {
            get;
        } // Constructor

        IEnumerable<FixedArgumentHandle> FixedArguments
        {
            get;
        } // FixedArguments

        IEnumerable<NamedArgumentHandle> NamedArguments
        {
            get;
        } // NamedArguments

        CustomAttributeHandle Handle
        {
            get;
        } // Handle
    } // ICustomAttribute

    /// <summary>
    /// CustomAttribute
    /// </summary>
    public partial struct CustomAttribute : ICustomAttribute
    {
    } // CustomAttribute

    /// <summary>
    /// ICustomAttributeHandle
    /// </summary>
    internal interface ICustomAttributeHandle : IEquatable<CustomAttributeHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ICustomAttributeHandle

    /// <summary>
    /// CustomAttributeHandle
    /// </summary>
    public partial struct CustomAttributeHandle : ICustomAttributeHandle
    {
    } // CustomAttributeHandle

    /// <summary>
    /// ICustomModifier
    /// </summary>
    internal interface ICustomModifier
    {
        bool IsOptional
        {
            get;
        } // IsOptional

        Handle Type
        {
            get;
        } // Type

        CustomModifierHandle Handle
        {
            get;
        } // Handle
    } // ICustomModifier

    /// <summary>
    /// CustomModifier
    /// </summary>
    public partial struct CustomModifier : ICustomModifier
    {
    } // CustomModifier

    /// <summary>
    /// ICustomModifierHandle
    /// </summary>
    internal interface ICustomModifierHandle : IEquatable<CustomModifierHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ICustomModifierHandle

    /// <summary>
    /// CustomModifierHandle
    /// </summary>
    public partial struct CustomModifierHandle : ICustomModifierHandle
    {
    } // CustomModifierHandle

    /// <summary>
    /// IEvent
    /// </summary>
    internal interface IEvent
    {
        EventAttributes Flags
        {
            get;
        } // Flags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        Handle Type
        {
            get;
        } // Type

        IEnumerable<MethodSemanticsHandle> MethodSemantics
        {
            get;
        } // MethodSemantics

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        EventHandle Handle
        {
            get;
        } // Handle
    } // IEvent

    /// <summary>
    /// Event
    /// </summary>
    public partial struct Event : IEvent
    {
    } // Event

    /// <summary>
    /// IEventHandle
    /// </summary>
    internal interface IEventHandle : IEquatable<EventHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IEventHandle

    /// <summary>
    /// EventHandle
    /// </summary>
    public partial struct EventHandle : IEventHandle
    {
    } // EventHandle

    /// <summary>
    /// IField
    /// </summary>
    internal interface IField
    {
        FieldAttributes Flags
        {
            get;
        } // Flags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        FieldSignatureHandle Signature
        {
            get;
        } // Signature

        Handle DefaultValue
        {
            get;
        } // DefaultValue

        uint Offset
        {
            get;
        } // Offset

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        FieldHandle Handle
        {
            get;
        } // Handle
    } // IField

    /// <summary>
    /// Field
    /// </summary>
    public partial struct Field : IField
    {
    } // Field

    /// <summary>
    /// IFieldHandle
    /// </summary>
    internal interface IFieldHandle : IEquatable<FieldHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IFieldHandle

    /// <summary>
    /// FieldHandle
    /// </summary>
    public partial struct FieldHandle : IFieldHandle
    {
    } // FieldHandle

    /// <summary>
    /// IFieldSignature
    /// </summary>
    internal interface IFieldSignature
    {
        Handle Type
        {
            get;
        } // Type

        IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get;
        } // CustomModifiers

        FieldSignatureHandle Handle
        {
            get;
        } // Handle
    } // IFieldSignature

    /// <summary>
    /// FieldSignature
    /// </summary>
    public partial struct FieldSignature : IFieldSignature
    {
    } // FieldSignature

    /// <summary>
    /// IFieldSignatureHandle
    /// </summary>
    internal interface IFieldSignatureHandle : IEquatable<FieldSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IFieldSignatureHandle

    /// <summary>
    /// FieldSignatureHandle
    /// </summary>
    public partial struct FieldSignatureHandle : IFieldSignatureHandle
    {
    } // FieldSignatureHandle

    /// <summary>
    /// IFixedArgument
    /// </summary>
    internal interface IFixedArgument
    {
        FixedArgumentAttributes Flags
        {
            get;
        } // Flags

        Handle Type
        {
            get;
        } // Type

        Handle Value
        {
            get;
        } // Value

        FixedArgumentHandle Handle
        {
            get;
        } // Handle
    } // IFixedArgument

    /// <summary>
    /// FixedArgument
    /// </summary>
    public partial struct FixedArgument : IFixedArgument
    {
    } // FixedArgument

    /// <summary>
    /// IFixedArgumentHandle
    /// </summary>
    internal interface IFixedArgumentHandle : IEquatable<FixedArgumentHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IFixedArgumentHandle

    /// <summary>
    /// FixedArgumentHandle
    /// </summary>
    public partial struct FixedArgumentHandle : IFixedArgumentHandle
    {
    } // FixedArgumentHandle

    /// <summary>
    /// IGenericParameter
    /// </summary>
    internal interface IGenericParameter
    {
        ushort Number
        {
            get;
        } // Number

        GenericParameterAttributes Flags
        {
            get;
        } // Flags

        GenericParameterKind Kind
        {
            get;
        } // Kind

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        IEnumerable<Handle> Constraints
        {
            get;
        } // Constraints

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        GenericParameterHandle Handle
        {
            get;
        } // Handle
    } // IGenericParameter

    /// <summary>
    /// GenericParameter
    /// </summary>
    public partial struct GenericParameter : IGenericParameter
    {
    } // GenericParameter

    /// <summary>
    /// IGenericParameterHandle
    /// </summary>
    internal interface IGenericParameterHandle : IEquatable<GenericParameterHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IGenericParameterHandle

    /// <summary>
    /// GenericParameterHandle
    /// </summary>
    public partial struct GenericParameterHandle : IGenericParameterHandle
    {
    } // GenericParameterHandle

    /// <summary>
    /// IHandle
    /// </summary>
    internal interface IHandle : IEquatable<Handle>, IEquatable<Object>
    {
        int GetHashCode();
        HandleType HandleType
        {
            get;
        } // HandleType

        PropertySignatureHandle ToPropertySignatureHandle(MetadataReader reader);
        MethodSemanticsHandle ToMethodSemanticsHandle(MetadataReader reader);
        ByReferenceSignatureHandle ToByReferenceSignatureHandle(MetadataReader reader);
        ConstantStringArrayHandle ToConstantStringArrayHandle(MetadataReader reader);
        ConstantInt64ValueHandle ToConstantInt64ValueHandle(MetadataReader reader);
        ConstantReferenceValueHandle ToConstantReferenceValueHandle(MetadataReader reader);
        NamespaceReferenceHandle ToNamespaceReferenceHandle(MetadataReader reader);
        ScopeDefinitionHandle ToScopeDefinitionHandle(MetadataReader reader);
        PointerSignatureHandle ToPointerSignatureHandle(MetadataReader reader);
        ReturnTypeSignatureHandle ToReturnTypeSignatureHandle(MetadataReader reader);
        ConstantSByteArrayHandle ToConstantSByteArrayHandle(MetadataReader reader);
        ConstantInt16ArrayHandle ToConstantInt16ArrayHandle(MetadataReader reader);
        ConstantStringValueHandle ToConstantStringValueHandle(MetadataReader reader);
        MethodTypeVariableSignatureHandle ToMethodTypeVariableSignatureHandle(MetadataReader reader);
        TypeForwarderHandle ToTypeForwarderHandle(MetadataReader reader);
        ConstantInt16ValueHandle ToConstantInt16ValueHandle(MetadataReader reader);
        ConstantUInt32ArrayHandle ToConstantUInt32ArrayHandle(MetadataReader reader);
        ConstantByteArrayHandle ToConstantByteArrayHandle(MetadataReader reader);
        FieldHandle ToFieldHandle(MetadataReader reader);
        NamedArgumentHandle ToNamedArgumentHandle(MetadataReader reader);
        TypeReferenceHandle ToTypeReferenceHandle(MetadataReader reader);
        ConstantHandleArrayHandle ToConstantHandleArrayHandle(MetadataReader reader);
        CustomAttributeHandle ToCustomAttributeHandle(MetadataReader reader);
        ConstantByteValueHandle ToConstantByteValueHandle(MetadataReader reader);
        ConstantSingleArrayHandle ToConstantSingleArrayHandle(MetadataReader reader);
        MemberReferenceHandle ToMemberReferenceHandle(MetadataReader reader);
        ArraySignatureHandle ToArraySignatureHandle(MetadataReader reader);
        MethodHandle ToMethodHandle(MetadataReader reader);
        ConstantUInt32ValueHandle ToConstantUInt32ValueHandle(MetadataReader reader);
        ConstantCharArrayHandle ToConstantCharArrayHandle(MetadataReader reader);
        TypeVariableSignatureHandle ToTypeVariableSignatureHandle(MetadataReader reader);
        ConstantCharValueHandle ToConstantCharValueHandle(MetadataReader reader);
        ScopeReferenceHandle ToScopeReferenceHandle(MetadataReader reader);
        MethodSignatureHandle ToMethodSignatureHandle(MetadataReader reader);
        ConstantBoxedEnumValueHandle ToConstantBoxedEnumValueHandle(MetadataReader reader);
        CustomModifierHandle ToCustomModifierHandle(MetadataReader reader);
        ConstantSingleValueHandle ToConstantSingleValueHandle(MetadataReader reader);
        ConstantSByteValueHandle ToConstantSByteValueHandle(MetadataReader reader);
        ConstantUInt16ArrayHandle ToConstantUInt16ArrayHandle(MetadataReader reader);
        ConstantUInt64ValueHandle ToConstantUInt64ValueHandle(MetadataReader reader);
        TypeDefinitionHandle ToTypeDefinitionHandle(MetadataReader reader);
        ConstantInt32ValueHandle ToConstantInt32ValueHandle(MetadataReader reader);
        ConstantInt64ArrayHandle ToConstantInt64ArrayHandle(MetadataReader reader);
        FixedArgumentHandle ToFixedArgumentHandle(MetadataReader reader);
        ParameterTypeSignatureHandle ToParameterTypeSignatureHandle(MetadataReader reader);
        PropertyHandle ToPropertyHandle(MetadataReader reader);
        ConstantDoubleArrayHandle ToConstantDoubleArrayHandle(MetadataReader reader);
        FieldSignatureHandle ToFieldSignatureHandle(MetadataReader reader);
        MethodInstantiationHandle ToMethodInstantiationHandle(MetadataReader reader);
        ConstantUInt64ArrayHandle ToConstantUInt64ArrayHandle(MetadataReader reader);
        ConstantBooleanValueHandle ToConstantBooleanValueHandle(MetadataReader reader);
        NamespaceDefinitionHandle ToNamespaceDefinitionHandle(MetadataReader reader);
        MethodImplHandle ToMethodImplHandle(MetadataReader reader);
        TypeSpecificationHandle ToTypeSpecificationHandle(MetadataReader reader);
        ConstantInt32ArrayHandle ToConstantInt32ArrayHandle(MetadataReader reader);
        EventHandle ToEventHandle(MetadataReader reader);
        ConstantUInt16ValueHandle ToConstantUInt16ValueHandle(MetadataReader reader);
        ConstantBooleanArrayHandle ToConstantBooleanArrayHandle(MetadataReader reader);
        GenericParameterHandle ToGenericParameterHandle(MetadataReader reader);
        TypeInstantiationSignatureHandle ToTypeInstantiationSignatureHandle(MetadataReader reader);
        SZArraySignatureHandle ToSZArraySignatureHandle(MetadataReader reader);
        ConstantDoubleValueHandle ToConstantDoubleValueHandle(MetadataReader reader);
        ParameterHandle ToParameterHandle(MetadataReader reader);
    } // IHandle

    /// <summary>
    /// Handle
    /// </summary>
    public partial struct Handle : IHandle
    {
    } // Handle

    /// <summary>
    /// IMemberReference
    /// </summary>
    internal interface IMemberReference
    {
        Handle Parent
        {
            get;
        } // Parent

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        Handle Signature
        {
            get;
        } // Signature

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        MemberReferenceHandle Handle
        {
            get;
        } // Handle
    } // IMemberReference

    /// <summary>
    /// MemberReference
    /// </summary>
    public partial struct MemberReference : IMemberReference
    {
    } // MemberReference

    /// <summary>
    /// IMemberReferenceHandle
    /// </summary>
    internal interface IMemberReferenceHandle : IEquatable<MemberReferenceHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMemberReferenceHandle

    /// <summary>
    /// MemberReferenceHandle
    /// </summary>
    public partial struct MemberReferenceHandle : IMemberReferenceHandle
    {
    } // MemberReferenceHandle

    /// <summary>
    /// IMetadataReader
    /// </summary>
    public interface IMetadataReader
    {
        ConstantInt64Value GetConstantInt64Value(ConstantInt64ValueHandle handle);
        Property GetProperty(PropertyHandle handle);
        SZArraySignature GetSZArraySignature(SZArraySignatureHandle handle);
        TypeDefinition GetTypeDefinition(TypeDefinitionHandle handle);
        ParameterTypeSignature GetParameterTypeSignature(ParameterTypeSignatureHandle handle);
        ConstantDoubleValue GetConstantDoubleValue(ConstantDoubleValueHandle handle);
        ConstantCharValue GetConstantCharValue(ConstantCharValueHandle handle);
        ConstantBooleanValue GetConstantBooleanValue(ConstantBooleanValueHandle handle);
        ConstantSingleArray GetConstantSingleArray(ConstantSingleArrayHandle handle);
        ConstantUInt64Array GetConstantUInt64Array(ConstantUInt64ArrayHandle handle);
        ConstantInt16Array GetConstantInt16Array(ConstantInt16ArrayHandle handle);
        ConstantSByteValue GetConstantSByteValue(ConstantSByteValueHandle handle);
        ConstantByteValue GetConstantByteValue(ConstantByteValueHandle handle);
        ScopeDefinition GetScopeDefinition(ScopeDefinitionHandle handle);
        ArraySignature GetArraySignature(ArraySignatureHandle handle);
        ConstantInt32Array GetConstantInt32Array(ConstantInt32ArrayHandle handle);
        ConstantUInt32Array GetConstantUInt32Array(ConstantUInt32ArrayHandle handle);
        ScopeReference GetScopeReference(ScopeReferenceHandle handle);
        MethodInstantiation GetMethodInstantiation(MethodInstantiationHandle handle);
        CustomModifier GetCustomModifier(CustomModifierHandle handle);
        ConstantDoubleArray GetConstantDoubleArray(ConstantDoubleArrayHandle handle);
        Event GetEvent(EventHandle handle);
        ConstantSByteArray GetConstantSByteArray(ConstantSByteArrayHandle handle);
        ReturnTypeSignature GetReturnTypeSignature(ReturnTypeSignatureHandle handle);
        ConstantBoxedEnumValue GetConstantBoxedEnumValue(ConstantBoxedEnumValueHandle handle);
        ConstantInt32Value GetConstantInt32Value(ConstantInt32ValueHandle handle);
        ConstantSingleValue GetConstantSingleValue(ConstantSingleValueHandle handle);
        Parameter GetParameter(ParameterHandle handle);
        ConstantUInt16Value GetConstantUInt16Value(ConstantUInt16ValueHandle handle);
        ConstantInt16Value GetConstantInt16Value(ConstantInt16ValueHandle handle);
        ConstantCharArray GetConstantCharArray(ConstantCharArrayHandle handle);
        TypeVariableSignature GetTypeVariableSignature(TypeVariableSignatureHandle handle);
        ConstantStringArray GetConstantStringArray(ConstantStringArrayHandle handle);
        ByReferenceSignature GetByReferenceSignature(ByReferenceSignatureHandle handle);
        TypeReference GetTypeReference(TypeReferenceHandle handle);
        ConstantUInt32Value GetConstantUInt32Value(ConstantUInt32ValueHandle handle);
        ConstantUInt64Value GetConstantUInt64Value(ConstantUInt64ValueHandle handle);
        MethodImpl GetMethodImpl(MethodImplHandle handle);
        MethodSemantics GetMethodSemantics(MethodSemanticsHandle handle);
        GenericParameter GetGenericParameter(GenericParameterHandle handle);
        MethodTypeVariableSignature GetMethodTypeVariableSignature(MethodTypeVariableSignatureHandle handle);
        TypeInstantiationSignature GetTypeInstantiationSignature(TypeInstantiationSignatureHandle handle);
        FieldSignature GetFieldSignature(FieldSignatureHandle handle);
        NamespaceReference GetNamespaceReference(NamespaceReferenceHandle handle);
        MethodSignature GetMethodSignature(MethodSignatureHandle handle);
        PointerSignature GetPointerSignature(PointerSignatureHandle handle);
        NamedArgument GetNamedArgument(NamedArgumentHandle handle);
        ConstantStringValue GetConstantStringValue(ConstantStringValueHandle handle);
        CustomAttribute GetCustomAttribute(CustomAttributeHandle handle);
        ConstantInt64Array GetConstantInt64Array(ConstantInt64ArrayHandle handle);
        MemberReference GetMemberReference(MemberReferenceHandle handle);
        ConstantReferenceValue GetConstantReferenceValue(ConstantReferenceValueHandle handle);
        FixedArgument GetFixedArgument(FixedArgumentHandle handle);
        Field GetField(FieldHandle handle);
        ConstantByteArray GetConstantByteArray(ConstantByteArrayHandle handle);
        ConstantHandleArray GetConstantHandleArray(ConstantHandleArrayHandle handle);
        Method GetMethod(MethodHandle handle);
        ConstantBooleanArray GetConstantBooleanArray(ConstantBooleanArrayHandle handle);
        TypeForwarder GetTypeForwarder(TypeForwarderHandle handle);
        NamespaceDefinition GetNamespaceDefinition(NamespaceDefinitionHandle handle);
        PropertySignature GetPropertySignature(PropertySignatureHandle handle);
        ConstantUInt16Array GetConstantUInt16Array(ConstantUInt16ArrayHandle handle);
        TypeSpecification GetTypeSpecification(TypeSpecificationHandle handle);
        IEnumerable<ScopeDefinitionHandle> ScopeDefinitions
        {
            get;
        } // ScopeDefinitions
        Handle NullHandle
        {
            get;
        } // NullHandle
    } // IMetadataReader

    /// <summary>
    /// MetadataReader
    /// </summary>
    public partial class MetadataReader : IMetadataReader
    {
    } // MetadataReader

    /// <summary>
    /// IMethod
    /// </summary>
    internal interface IMethod
    {
        uint RVA
        {
            get;
        } // RVA

        MethodAttributes Flags
        {
            get;
        } // Flags

        MethodImplAttributes ImplFlags
        {
            get;
        } // ImplFlags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        MethodSignatureHandle Signature
        {
            get;
        } // Signature

        IEnumerable<ParameterHandle> Parameters
        {
            get;
        } // Parameters

        IEnumerable<GenericParameterHandle> GenericParameters
        {
            get;
        } // GenericParameters

        IEnumerable<MethodImplHandle> MethodImpls
        {
            get;
        } // MethodImpls

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        MethodHandle Handle
        {
            get;
        } // Handle
    } // IMethod

    /// <summary>
    /// Method
    /// </summary>
    public partial struct Method : IMethod
    {
    } // Method

    /// <summary>
    /// IMethodHandle
    /// </summary>
    internal interface IMethodHandle : IEquatable<MethodHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMethodHandle

    /// <summary>
    /// MethodHandle
    /// </summary>
    public partial struct MethodHandle : IMethodHandle
    {
    } // MethodHandle

    /// <summary>
    /// IMethodImpl
    /// </summary>
    internal interface IMethodImpl
    {
        Handle MethodDeclaration
        {
            get;
        } // MethodDeclaration

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        MethodImplHandle Handle
        {
            get;
        } // Handle
    } // IMethodImpl

    /// <summary>
    /// MethodImpl
    /// </summary>
    public partial struct MethodImpl : IMethodImpl
    {
    } // MethodImpl

    /// <summary>
    /// IMethodImplHandle
    /// </summary>
    internal interface IMethodImplHandle : IEquatable<MethodImplHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMethodImplHandle

    /// <summary>
    /// MethodImplHandle
    /// </summary>
    public partial struct MethodImplHandle : IMethodImplHandle
    {
    } // MethodImplHandle

    /// <summary>
    /// IMethodInstantiation
    /// </summary>
    internal interface IMethodInstantiation
    {
        Handle Method
        {
            get;
        } // Method

        MethodSignatureHandle Instantiation
        {
            get;
        } // Instantiation

        MethodInstantiationHandle Handle
        {
            get;
        } // Handle
    } // IMethodInstantiation

    /// <summary>
    /// MethodInstantiation
    /// </summary>
    public partial struct MethodInstantiation : IMethodInstantiation
    {
    } // MethodInstantiation

    /// <summary>
    /// IMethodInstantiationHandle
    /// </summary>
    internal interface IMethodInstantiationHandle : IEquatable<MethodInstantiationHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMethodInstantiationHandle

    /// <summary>
    /// MethodInstantiationHandle
    /// </summary>
    public partial struct MethodInstantiationHandle : IMethodInstantiationHandle
    {
    } // MethodInstantiationHandle

    /// <summary>
    /// IMethodSemantics
    /// </summary>
    internal interface IMethodSemantics
    {
        MethodSemanticsAttributes Attributes
        {
            get;
        } // Attributes

        MethodHandle Method
        {
            get;
        } // Method

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        MethodSemanticsHandle Handle
        {
            get;
        } // Handle
    } // IMethodSemantics

    /// <summary>
    /// MethodSemantics
    /// </summary>
    public partial struct MethodSemantics : IMethodSemantics
    {
    } // MethodSemantics

    /// <summary>
    /// IMethodSemanticsHandle
    /// </summary>
    internal interface IMethodSemanticsHandle : IEquatable<MethodSemanticsHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMethodSemanticsHandle

    /// <summary>
    /// MethodSemanticsHandle
    /// </summary>
    public partial struct MethodSemanticsHandle : IMethodSemanticsHandle
    {
    } // MethodSemanticsHandle

    /// <summary>
    /// IMethodSignature
    /// </summary>
    internal interface IMethodSignature
    {
        CallingConventions CallingConvention
        {
            get;
        } // CallingConvention

        int GenericParameterCount
        {
            get;
        } // GenericParameterCount

        ReturnTypeSignatureHandle ReturnType
        {
            get;
        } // ReturnType

        IEnumerable<ParameterTypeSignatureHandle> Parameters
        {
            get;
        } // Parameters

        IEnumerable<ParameterTypeSignatureHandle> VarArgParameters
        {
            get;
        } // VarArgParameters

        MethodSignatureHandle Handle
        {
            get;
        } // Handle
    } // IMethodSignature

    /// <summary>
    /// MethodSignature
    /// </summary>
    public partial struct MethodSignature : IMethodSignature
    {
    } // MethodSignature

    /// <summary>
    /// IMethodSignatureHandle
    /// </summary>
    internal interface IMethodSignatureHandle : IEquatable<MethodSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMethodSignatureHandle

    /// <summary>
    /// MethodSignatureHandle
    /// </summary>
    public partial struct MethodSignatureHandle : IMethodSignatureHandle
    {
    } // MethodSignatureHandle

    /// <summary>
    /// IMethodTypeVariableSignature
    /// </summary>
    internal interface IMethodTypeVariableSignature
    {
        int Number
        {
            get;
        } // Number

        MethodTypeVariableSignatureHandle Handle
        {
            get;
        } // Handle
    } // IMethodTypeVariableSignature

    /// <summary>
    /// MethodTypeVariableSignature
    /// </summary>
    public partial struct MethodTypeVariableSignature : IMethodTypeVariableSignature
    {
    } // MethodTypeVariableSignature

    /// <summary>
    /// IMethodTypeVariableSignatureHandle
    /// </summary>
    internal interface IMethodTypeVariableSignatureHandle : IEquatable<MethodTypeVariableSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IMethodTypeVariableSignatureHandle

    /// <summary>
    /// MethodTypeVariableSignatureHandle
    /// </summary>
    public partial struct MethodTypeVariableSignatureHandle : IMethodTypeVariableSignatureHandle
    {
    } // MethodTypeVariableSignatureHandle

    /// <summary>
    /// INamedArgument
    /// </summary>
    internal interface INamedArgument
    {
        NamedArgumentMemberKind Flags
        {
            get;
        } // Flags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        FixedArgumentHandle Value
        {
            get;
        } // Value

        NamedArgumentHandle Handle
        {
            get;
        } // Handle
    } // INamedArgument

    /// <summary>
    /// NamedArgument
    /// </summary>
    public partial struct NamedArgument : INamedArgument
    {
    } // NamedArgument

    /// <summary>
    /// INamedArgumentHandle
    /// </summary>
    internal interface INamedArgumentHandle : IEquatable<NamedArgumentHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // INamedArgumentHandle

    /// <summary>
    /// NamedArgumentHandle
    /// </summary>
    public partial struct NamedArgumentHandle : INamedArgumentHandle
    {
    } // NamedArgumentHandle

    /// <summary>
    /// INamespaceDefinition
    /// </summary>
    internal interface INamespaceDefinition
    {
        Handle ParentScopeOrNamespace
        {
            get;
        } // ParentScopeOrNamespace

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        IEnumerable<TypeDefinitionHandle> TypeDefinitions
        {
            get;
        } // TypeDefinitions

        IEnumerable<TypeForwarderHandle> TypeForwarders
        {
            get;
        } // TypeForwarders

        IEnumerable<NamespaceDefinitionHandle> NamespaceDefinitions
        {
            get;
        } // NamespaceDefinitions

        NamespaceDefinitionHandle Handle
        {
            get;
        } // Handle
    } // INamespaceDefinition

    /// <summary>
    /// NamespaceDefinition
    /// </summary>
    public partial struct NamespaceDefinition : INamespaceDefinition
    {
    } // NamespaceDefinition

    /// <summary>
    /// INamespaceDefinitionHandle
    /// </summary>
    internal interface INamespaceDefinitionHandle : IEquatable<NamespaceDefinitionHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // INamespaceDefinitionHandle

    /// <summary>
    /// NamespaceDefinitionHandle
    /// </summary>
    public partial struct NamespaceDefinitionHandle : INamespaceDefinitionHandle
    {
    } // NamespaceDefinitionHandle

    /// <summary>
    /// INamespaceReference
    /// </summary>
    internal interface INamespaceReference
    {
        Handle ParentScopeOrNamespace
        {
            get;
        } // ParentScopeOrNamespace

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        NamespaceReferenceHandle Handle
        {
            get;
        } // Handle
    } // INamespaceReference

    /// <summary>
    /// NamespaceReference
    /// </summary>
    public partial struct NamespaceReference : INamespaceReference
    {
    } // NamespaceReference

    /// <summary>
    /// INamespaceReferenceHandle
    /// </summary>
    internal interface INamespaceReferenceHandle : IEquatable<NamespaceReferenceHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // INamespaceReferenceHandle

    /// <summary>
    /// NamespaceReferenceHandle
    /// </summary>
    public partial struct NamespaceReferenceHandle : INamespaceReferenceHandle
    {
    } // NamespaceReferenceHandle

    /// <summary>
    /// IParameter
    /// </summary>
    internal interface IParameter
    {
        ParameterAttributes Flags
        {
            get;
        } // Flags

        ushort Sequence
        {
            get;
        } // Sequence

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        Handle DefaultValue
        {
            get;
        } // DefaultValue

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        ParameterHandle Handle
        {
            get;
        } // Handle
    } // IParameter

    /// <summary>
    /// Parameter
    /// </summary>
    public partial struct Parameter : IParameter
    {
    } // Parameter

    /// <summary>
    /// IParameterHandle
    /// </summary>
    internal interface IParameterHandle : IEquatable<ParameterHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IParameterHandle

    /// <summary>
    /// ParameterHandle
    /// </summary>
    public partial struct ParameterHandle : IParameterHandle
    {
    } // ParameterHandle

    /// <summary>
    /// IParameterTypeSignature
    /// </summary>
    internal interface IParameterTypeSignature
    {
        IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get;
        } // CustomModifiers

        Handle Type
        {
            get;
        } // Type

        ParameterTypeSignatureHandle Handle
        {
            get;
        } // Handle
    } // IParameterTypeSignature

    /// <summary>
    /// ParameterTypeSignature
    /// </summary>
    public partial struct ParameterTypeSignature : IParameterTypeSignature
    {
    } // ParameterTypeSignature

    /// <summary>
    /// IParameterTypeSignatureHandle
    /// </summary>
    internal interface IParameterTypeSignatureHandle : IEquatable<ParameterTypeSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IParameterTypeSignatureHandle

    /// <summary>
    /// ParameterTypeSignatureHandle
    /// </summary>
    public partial struct ParameterTypeSignatureHandle : IParameterTypeSignatureHandle
    {
    } // ParameterTypeSignatureHandle

    /// <summary>
    /// IPointerSignature
    /// </summary>
    internal interface IPointerSignature
    {
        Handle Type
        {
            get;
        } // Type

        PointerSignatureHandle Handle
        {
            get;
        } // Handle
    } // IPointerSignature

    /// <summary>
    /// PointerSignature
    /// </summary>
    public partial struct PointerSignature : IPointerSignature
    {
    } // PointerSignature

    /// <summary>
    /// IPointerSignatureHandle
    /// </summary>
    internal interface IPointerSignatureHandle : IEquatable<PointerSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IPointerSignatureHandle

    /// <summary>
    /// PointerSignatureHandle
    /// </summary>
    public partial struct PointerSignatureHandle : IPointerSignatureHandle
    {
    } // PointerSignatureHandle

    /// <summary>
    /// IProperty
    /// </summary>
    internal interface IProperty
    {
        PropertyAttributes Flags
        {
            get;
        } // Flags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        PropertySignatureHandle Signature
        {
            get;
        } // Signature

        IEnumerable<MethodSemanticsHandle> MethodSemantics
        {
            get;
        } // MethodSemantics

        Handle DefaultValue
        {
            get;
        } // DefaultValue

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        PropertyHandle Handle
        {
            get;
        } // Handle
    } // IProperty

    /// <summary>
    /// Property
    /// </summary>
    public partial struct Property : IProperty
    {
    } // Property

    /// <summary>
    /// IPropertyHandle
    /// </summary>
    internal interface IPropertyHandle : IEquatable<PropertyHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IPropertyHandle

    /// <summary>
    /// PropertyHandle
    /// </summary>
    public partial struct PropertyHandle : IPropertyHandle
    {
    } // PropertyHandle

    /// <summary>
    /// IPropertySignature
    /// </summary>
    internal interface IPropertySignature
    {
        CallingConventions CallingConvention
        {
            get;
        } // CallingConvention

        IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get;
        } // CustomModifiers

        Handle Type
        {
            get;
        } // Type

        IEnumerable<ParameterTypeSignatureHandle> Parameters
        {
            get;
        } // Parameters

        PropertySignatureHandle Handle
        {
            get;
        } // Handle
    } // IPropertySignature

    /// <summary>
    /// PropertySignature
    /// </summary>
    public partial struct PropertySignature : IPropertySignature
    {
    } // PropertySignature

    /// <summary>
    /// IPropertySignatureHandle
    /// </summary>
    internal interface IPropertySignatureHandle : IEquatable<PropertySignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IPropertySignatureHandle

    /// <summary>
    /// PropertySignatureHandle
    /// </summary>
    public partial struct PropertySignatureHandle : IPropertySignatureHandle
    {
    } // PropertySignatureHandle

    /// <summary>
    /// IReturnTypeSignature
    /// </summary>
    internal interface IReturnTypeSignature
    {
        IEnumerable<CustomModifierHandle> CustomModifiers
        {
            get;
        } // CustomModifiers

        Handle Type
        {
            get;
        } // Type

        ReturnTypeSignatureHandle Handle
        {
            get;
        } // Handle
    } // IReturnTypeSignature

    /// <summary>
    /// ReturnTypeSignature
    /// </summary>
    public partial struct ReturnTypeSignature : IReturnTypeSignature
    {
    } // ReturnTypeSignature

    /// <summary>
    /// IReturnTypeSignatureHandle
    /// </summary>
    internal interface IReturnTypeSignatureHandle : IEquatable<ReturnTypeSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IReturnTypeSignatureHandle

    /// <summary>
    /// ReturnTypeSignatureHandle
    /// </summary>
    public partial struct ReturnTypeSignatureHandle : IReturnTypeSignatureHandle
    {
    } // ReturnTypeSignatureHandle

    /// <summary>
    /// ISZArraySignature
    /// </summary>
    internal interface ISZArraySignature
    {
        Handle ElementType
        {
            get;
        } // ElementType

        SZArraySignatureHandle Handle
        {
            get;
        } // Handle
    } // ISZArraySignature

    /// <summary>
    /// SZArraySignature
    /// </summary>
    public partial struct SZArraySignature : ISZArraySignature
    {
    } // SZArraySignature

    /// <summary>
    /// ISZArraySignatureHandle
    /// </summary>
    internal interface ISZArraySignatureHandle : IEquatable<SZArraySignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ISZArraySignatureHandle

    /// <summary>
    /// SZArraySignatureHandle
    /// </summary>
    public partial struct SZArraySignatureHandle : ISZArraySignatureHandle
    {
    } // SZArraySignatureHandle

    /// <summary>
    /// IScopeDefinition
    /// </summary>
    internal interface IScopeDefinition
    {
        AssemblyFlags Flags
        {
            get;
        } // Flags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        AssemblyHashAlgorithm HashAlgorithm
        {
            get;
        } // HashAlgorithm

        ushort MajorVersion
        {
            get;
        } // MajorVersion

        ushort MinorVersion
        {
            get;
        } // MinorVersion

        ushort BuildNumber
        {
            get;
        } // BuildNumber

        ushort RevisionNumber
        {
            get;
        } // RevisionNumber

        IEnumerable<byte> PublicKey
        {
            get;
        } // PublicKey

        ConstantStringValueHandle Culture
        {
            get;
        } // Culture

        NamespaceDefinitionHandle RootNamespaceDefinition
        {
            get;
        } // RootNamespaceDefinition

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        ScopeDefinitionHandle Handle
        {
            get;
        } // Handle
    } // IScopeDefinition

    /// <summary>
    /// ScopeDefinition
    /// </summary>
    public partial struct ScopeDefinition : IScopeDefinition
    {
    } // ScopeDefinition

    /// <summary>
    /// IScopeDefinitionHandle
    /// </summary>
    internal interface IScopeDefinitionHandle : IEquatable<ScopeDefinitionHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IScopeDefinitionHandle

    /// <summary>
    /// ScopeDefinitionHandle
    /// </summary>
    public partial struct ScopeDefinitionHandle : IScopeDefinitionHandle
    {
    } // ScopeDefinitionHandle

    /// <summary>
    /// IScopeReference
    /// </summary>
    internal interface IScopeReference
    {
        AssemblyFlags Flags
        {
            get;
        } // Flags

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        ushort MajorVersion
        {
            get;
        } // MajorVersion

        ushort MinorVersion
        {
            get;
        } // MinorVersion

        ushort BuildNumber
        {
            get;
        } // BuildNumber

        ushort RevisionNumber
        {
            get;
        } // RevisionNumber

        IEnumerable<byte> PublicKeyOrToken
        {
            get;
        } // PublicKeyOrToken

        ConstantStringValueHandle Culture
        {
            get;
        } // Culture

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        ScopeReferenceHandle Handle
        {
            get;
        } // Handle
    } // IScopeReference

    /// <summary>
    /// ScopeReference
    /// </summary>
    public partial struct ScopeReference : IScopeReference
    {
    } // ScopeReference

    /// <summary>
    /// IScopeReferenceHandle
    /// </summary>
    internal interface IScopeReferenceHandle : IEquatable<ScopeReferenceHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // IScopeReferenceHandle

    /// <summary>
    /// ScopeReferenceHandle
    /// </summary>
    public partial struct ScopeReferenceHandle : IScopeReferenceHandle
    {
    } // ScopeReferenceHandle

    /// <summary>
    /// ITypeDefinition
    /// </summary>
    internal interface ITypeDefinition
    {
        TypeAttributes Flags
        {
            get;
        } // Flags

        Handle BaseType
        {
            get;
        } // BaseType

        NamespaceDefinitionHandle NamespaceDefinition
        {
            get;
        } // NamespaceDefinition

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        uint Size
        {
            get;
        } // Size

        uint PackingSize
        {
            get;
        } // PackingSize

        TypeDefinitionHandle EnclosingType
        {
            get;
        } // EnclosingType

        IEnumerable<TypeDefinitionHandle> NestedTypes
        {
            get;
        } // NestedTypes

        IEnumerable<MethodHandle> Methods
        {
            get;
        } // Methods

        IEnumerable<FieldHandle> Fields
        {
            get;
        } // Fields

        IEnumerable<PropertyHandle> Properties
        {
            get;
        } // Properties

        IEnumerable<EventHandle> Events
        {
            get;
        } // Events

        IEnumerable<GenericParameterHandle> GenericParameters
        {
            get;
        } // GenericParameters

        IEnumerable<Handle> Interfaces
        {
            get;
        } // Interfaces

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        TypeDefinitionHandle Handle
        {
            get;
        } // Handle
    } // ITypeDefinition

    /// <summary>
    /// TypeDefinition
    /// </summary>
    public partial struct TypeDefinition : ITypeDefinition
    {
    } // TypeDefinition

    /// <summary>
    /// ITypeDefinitionHandle
    /// </summary>
    internal interface ITypeDefinitionHandle : IEquatable<TypeDefinitionHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ITypeDefinitionHandle

    /// <summary>
    /// TypeDefinitionHandle
    /// </summary>
    public partial struct TypeDefinitionHandle : ITypeDefinitionHandle
    {
    } // TypeDefinitionHandle

    /// <summary>
    /// ITypeForwarder
    /// </summary>
    internal interface ITypeForwarder
    {
        ScopeReferenceHandle Scope
        {
            get;
        } // Scope

        ConstantStringValueHandle Name
        {
            get;
        } // Name

        IEnumerable<TypeForwarderHandle> NestedTypes
        {
            get;
        } // NestedTypes

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        TypeForwarderHandle Handle
        {
            get;
        } // Handle
    } // ITypeForwarder

    /// <summary>
    /// TypeForwarder
    /// </summary>
    public partial struct TypeForwarder : ITypeForwarder
    {
    } // TypeForwarder

    /// <summary>
    /// ITypeForwarderHandle
    /// </summary>
    internal interface ITypeForwarderHandle : IEquatable<TypeForwarderHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ITypeForwarderHandle

    /// <summary>
    /// TypeForwarderHandle
    /// </summary>
    public partial struct TypeForwarderHandle : ITypeForwarderHandle
    {
    } // TypeForwarderHandle

    /// <summary>
    /// ITypeInstantiationSignature
    /// </summary>
    internal interface ITypeInstantiationSignature
    {
        Handle GenericType
        {
            get;
        } // GenericType

        IEnumerable<Handle> GenericTypeArguments
        {
            get;
        } // GenericTypeArguments

        TypeInstantiationSignatureHandle Handle
        {
            get;
        } // Handle
    } // ITypeInstantiationSignature

    /// <summary>
    /// TypeInstantiationSignature
    /// </summary>
    public partial struct TypeInstantiationSignature : ITypeInstantiationSignature
    {
    } // TypeInstantiationSignature

    /// <summary>
    /// ITypeInstantiationSignatureHandle
    /// </summary>
    internal interface ITypeInstantiationSignatureHandle : IEquatable<TypeInstantiationSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ITypeInstantiationSignatureHandle

    /// <summary>
    /// TypeInstantiationSignatureHandle
    /// </summary>
    public partial struct TypeInstantiationSignatureHandle : ITypeInstantiationSignatureHandle
    {
    } // TypeInstantiationSignatureHandle

    /// <summary>
    /// ITypeReference
    /// </summary>
    internal interface ITypeReference
    {
        Handle ParentNamespaceOrType
        {
            get;
        } // ParentNamespaceOrType

        ConstantStringValueHandle TypeName
        {
            get;
        } // TypeName

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        TypeReferenceHandle Handle
        {
            get;
        } // Handle
    } // ITypeReference

    /// <summary>
    /// TypeReference
    /// </summary>
    public partial struct TypeReference : ITypeReference
    {
    } // TypeReference

    /// <summary>
    /// ITypeReferenceHandle
    /// </summary>
    internal interface ITypeReferenceHandle : IEquatable<TypeReferenceHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ITypeReferenceHandle

    /// <summary>
    /// TypeReferenceHandle
    /// </summary>
    public partial struct TypeReferenceHandle : ITypeReferenceHandle
    {
    } // TypeReferenceHandle

    /// <summary>
    /// ITypeSpecification
    /// </summary>
    internal interface ITypeSpecification
    {
        Handle Signature
        {
            get;
        } // Signature

        IEnumerable<CustomAttributeHandle> CustomAttributes
        {
            get;
        } // CustomAttributes

        TypeSpecificationHandle Handle
        {
            get;
        } // Handle
    } // ITypeSpecification

    /// <summary>
    /// TypeSpecification
    /// </summary>
    public partial struct TypeSpecification : ITypeSpecification
    {
    } // TypeSpecification

    /// <summary>
    /// ITypeSpecificationHandle
    /// </summary>
    internal interface ITypeSpecificationHandle : IEquatable<TypeSpecificationHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ITypeSpecificationHandle

    /// <summary>
    /// TypeSpecificationHandle
    /// </summary>
    public partial struct TypeSpecificationHandle : ITypeSpecificationHandle
    {
    } // TypeSpecificationHandle

    /// <summary>
    /// ITypeVariableSignature
    /// </summary>
    internal interface ITypeVariableSignature
    {
        int Number
        {
            get;
        } // Number

        TypeVariableSignatureHandle Handle
        {
            get;
        } // Handle
    } // ITypeVariableSignature

    /// <summary>
    /// TypeVariableSignature
    /// </summary>
    public partial struct TypeVariableSignature : ITypeVariableSignature
    {
    } // TypeVariableSignature

    /// <summary>
    /// ITypeVariableSignatureHandle
    /// </summary>
    internal interface ITypeVariableSignatureHandle : IEquatable<TypeVariableSignatureHandle>, IEquatable<Handle>, IEquatable<Object>
    {
        Handle ToHandle(MetadataReader reader);
        int GetHashCode();
    } // ITypeVariableSignatureHandle

    /// <summary>
    /// TypeVariableSignatureHandle
    /// </summary>
    public partial struct TypeVariableSignatureHandle : ITypeVariableSignatureHandle
    {
    } // TypeVariableSignatureHandle
} // Internal.Metadata.NativeFormat
