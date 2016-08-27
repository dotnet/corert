// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime
{
    /// <summary>
    /// Represents the flags stored in the <c>_usFlags</c> field of a <c>System.Runtime.EEType</c>.
    /// </summary>
    [Flags]
    internal enum EETypeFlags : ushort
    {
        /// <summary>
        /// There are four kinds of EETypes, defined in <c>Kinds</c>.
        /// </summary>
        EETypeKindMask = 0x0003,

        /// <summary>
        /// This flag is set when m_RelatedType is in a different module.  In that case, _pRelatedType
        /// actually points to an IAT slot in this module, which then points to the desired EEType in the
        /// other module.  In other words, there is an extra indirection through m_RelatedType to get to 
        /// the related type in the other module.  When this flag is set, it is expected that you use the 
        /// "_ppXxxxViaIAT" member of the RelatedTypeUnion for the particular related type you're 
        /// accessing.
        /// </summary>
        RelatedTypeViaIATFlag = 0x0004,

        /// <summary>
        /// This EEType represents a value type.
        /// </summary>
        ValueTypeFlag = 0x0008,

        /// <summary>
        /// This EEType represents a type which requires finalization.
        /// </summary>
        HasFinalizerFlag = 0x0010,

        /// <summary>
        /// This type contain GC pointers.
        /// </summary>
        HasPointersFlag = 0x0020,

        /// <summary>
        /// This type instance was allocated at runtime (rather than being embedded in a module image).
        /// </summary>
        RuntimeAllocatedFlag = 0x0040,

        /// <summary>
        /// This type is generic and one or more of its type parameters is co- or contra-variant. This
        /// only applies to interface and delegate types.
        /// </summary>
        GenericVarianceFlag = 0x0080,

        /// <summary>
        /// This type has optional fields present.
        /// </summary>
        OptionalFieldsFlag = 0x0100,

        /// <summary>
        /// This EEType represents an interface.
        /// </summary>
        IsInterfaceFlag = 0x0200,

        /// <summary>
        /// This type is generic.
        /// </summary>
        IsGenericFlag = 0x0400,

        /// <summary>
        /// We are storing a CorElementType in the upper bits for unboxing enums.
        /// </summary>
        CorElementTypeMask = 0xf800,
        CorElementTypeShift = 11,

        /// <summary>
        /// Single mark to check TypeKind and two flags. When non-zero, casting is more complicated.
        /// </summary>
        ComplexCastingMask = EETypeKindMask | RelatedTypeViaIATFlag | GenericVarianceFlag
    };

    internal enum EETypeKind : ushort
    {
        /// <summary>
        /// Represents a standard ECMA type
        /// </summary>
        CanonicalEEType = 0x0000,

        /// <summary>
        /// Represents a type cloned from another EEType
        /// </summary>
        ClonedEEType = 0x0001,

        /// <summary>
        /// Represents a paramaterized type. For example a single dimensional array or pointer type
        /// </summary>
        ParameterizedEEType = 0x0002,

        /// <summary>
        /// Represents an uninstantiated generic type definition
        /// </summary>
        GenericTypeDefEEType = 0x0003,
    }

    /// <summary>
    /// These are flag values that are rarely set for types. If any of them are set then an optional field will
    /// be associated with the EEType to represent them.
    /// </summary>
    [Flags]
    internal enum EETypeRareFlags : int
    {
        /// <summary>
        /// This type requires 8-byte alignment for its fields on certain platforms (only ARM currently).
        /// </summary>
        RequiresAlign8Flag = 0x00000001,

        /// <summary>
        /// Type implements ICastable to allow dynamic resolution of interface casts.
        /// </summary>
        ICastableFlag = 0x00000002,

        /// <summary>
        /// Type is an instantiation of Nullable<T>.
        /// </summary>
        IsNullableFlag = 0x00000004,

        /// <summary>
        /// Nullable target type stashed in the EEType is indirected via the IAT.
        /// </summary>
        NullableTypeViaIATFlag = 0x00000008,

        /// <summary>
        /// This EEType was created by generic instantiation loader
        /// </summary>
        IsDynamicTypeFlag = 0x00000010,

        /// <summary>
        /// This EEType has a Class Constructor
        /// </summary>
        HasCctorFlag = 0x0000020,

        /// <summary>
        /// This EEType has sealed vtable entries (note that this flag is only used for
        /// dynamically created types because they always have an optional field (hence the
        /// very explicit flag name).
        /// </summary>
        IsDynamicTypeWithSealedVTableEntriesFlag = 0x00000040,

        /// <summary>
        /// This EEType was constructed from a universal canonical template, and has
        /// its own dynamically created DispatchMap (does not use the DispatchMap of its template type)
        /// </summary>
        HasDynamicallyAllocatedDispatchMapFlag = 0x00000080,

        /// <summary>
        /// This EEType represents a structure that is an HFA
        /// </summary>
        IsHFAFlag = 0x00000100,

        /// <summary>
        /// This EEType has sealed vtable entries
        /// This is for statically generated types - we need two different flags because
        /// the sealed vtable entries are reached in different ways in the static and dynamic case
        /// </summary>
        HasSealedVTableEntriesFlag = 0x00000200,

        /// <summary>
        /// This dynamically created types has gc statics
        /// </summary>
        IsDynamicTypeWithGcStatics = 0x00000400,

        /// <summary>
        /// This dynamically created types has non gc statics
        /// </summary>
        IsDynamicTypeWithNonGcStatics = 0x00000800,

        /// <summary>
        /// This dynamically created types has thread statics
        /// </summary>
        IsDynamicTypeWithThreadStatics = 0x00001000,
    }

    internal enum EETypeOptionalFieldsElement : byte
    {
        /// <summary>
        /// Extra <c>EEType</c> flags not commonly used such as HasClassConstructor
        /// </summary>
        RareFlags,

        /// <summary>
        /// VTable slot of <see cref="ICastable.IsInstanceOfInterface"/> for direct invocation without interface dispatch overhead
        /// </summary>
        ICastableIsInstSlot,

        /// <summary>
        /// Index of the dispatch map pointer in the DispathMap table
        /// </summary>
        DispatchMap,

        /// <summary>
        /// Padding added to a value type when allocated on the GC heap
        /// </summary>
        ValueTypeFieldPadding,

        /// <summary>
        /// VTable slot of <see cref="ICastable.GetImplType"/> for direct invocation without interface dispatch overhead
        /// </summary>
        ICastableGetImplTypeSlot,

        /// <summary>
        /// Offset in Nullable&lt;T&gt; of the value field
        /// </summary>
        NullableValueOffset,

        // Number of field types we support
        Count
    }

    internal enum EETypeField
    {
        ETF_InterfaceMap,
        ETF_Finalizer,
        ETF_OptionalFieldsPtr,
        ETF_NullableType,
        ETF_SealedVirtualSlots,
        ETF_DynamicTemplateType,
        ETF_DynamicDispatchMap,
        ETF_GenericDefinition,
        ETF_GenericComposition,
    }

    internal enum CorElementType
    {
        ELEMENT_TYPE_END = 0x00,

        ELEMENT_TYPE_VOID = 0x1,
        ELEMENT_TYPE_BOOLEAN = 0x2,
        ELEMENT_TYPE_CHAR = 0x3,
        ELEMENT_TYPE_I1 = 0x4,
        ELEMENT_TYPE_U1 = 0x5,
        ELEMENT_TYPE_I2 = 0x6,
        ELEMENT_TYPE_U2 = 0x7,
        ELEMENT_TYPE_I4 = 0x8,
        ELEMENT_TYPE_U4 = 0x9,
        ELEMENT_TYPE_I8 = 0xa,
        ELEMENT_TYPE_U8 = 0xb,
        ELEMENT_TYPE_R4 = 0xc,
        ELEMENT_TYPE_R8 = 0xd,
        ELEMENT_TYPE_STRING = 0xe,
        ELEMENT_TYPE_PTR = 0xf,
        ELEMENT_TYPE_BYREF = 0x10,
        ELEMENT_TYPE_VALUETYPE = 0x11,
        ELEMENT_TYPE_CLASS = 0x12,

        ELEMENT_TYPE_ARRAY = 0x14,

        ELEMENT_TYPE_TYPEDBYREF = 0x16,
        ELEMENT_TYPE_I = 0x18,
        ELEMENT_TYPE_U = 0x19,
        ELEMENT_TYPE_FNPTR = 0x1b,
        ELEMENT_TYPE_OBJECT = 0x1c,
        ELEMENT_TYPE_SZARRAY = 0x1d,
    }

    internal enum EETypeOptionalFieldTag : byte
    {
        OFT_RareFlags,
        OFT_ICastableIsInstSlot,
        OFT_DispatchMap,
        OFT_ValueTypeFieldPadding,
        OFT_ICastableGetImplTypeSlot,
        OFT_NullableValueOffset,

        OFT_Count // Number of field types we support
    }

    // Keep this synchronized with GenericVarianceType in rhbinder.h.
    internal enum GenericVariance : byte
    {
        NonVariant = 0,
        Covariant = 1,
        Contravariant = 2,
        ArrayCovariant = 0x20,
    }
}
