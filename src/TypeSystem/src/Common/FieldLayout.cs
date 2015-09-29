// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Debug = System.Diagnostics.Debug;
using Interlocked = System.Threading.Interlocked;

namespace Internal.TypeSystem
{
    public struct InstanceFieldMason
    {
        private MetadataType _type;
        private int _numInstanceFields;

        private ComputedInstanceFieldLayout _computedLayout;

        private InstanceFieldMason(MetadataType type, int numInstanceFields)
        {
            _type = type;
            _computedLayout = new ComputedInstanceFieldLayout();
            _numInstanceFields = numInstanceFields;
        }

        public static ComputedInstanceFieldLayout ComputeFieldLayout(MetadataType type)
        {
            // CLI - Partition 1, section 9.5 - Generic types shall not be marked explicitlayout.  
            if (type.HasInstantiation && type.IsExplicitLayout)
            {
                throw new TypeLoadException();
            }

            // Count the number of instance fields in advance for convenience
            int numInstanceFields = 0;
            foreach (var field in type.GetFields())
                if (!field.IsStatic)
                    numInstanceFields++;

            if (type.IsModuleType)
            {
                // This is a global type, it must not have instance fields.
                if (numInstanceFields > 0)
                {
                    throw new TypeLoadException();
                }

                // Global types do not do the rest of instance field layout.
                ComputedInstanceFieldLayout result = new ComputedInstanceFieldLayout();
                result.PackValue = type.Context.Target.DefaultPackingSize;
                return result;
            }

            // CLI - Partition 2, section 22.8
            // A type has layout if it is marked SequentialLayout or ExplicitLayout.  If any type within an inheritance chain has layout, 
            // then so shall all its base classes, up to the one that descends immediately from System.ValueType (if it exists in the type’s 
            // hierarchy); otherwise, from System.Object
            // Note: While the CLI isn't clearly worded, the layout needs to be the same for the entire chain.
            // If the current type isn't ValueType or System.Object and has a layout and the parent type isn't
            // ValueType or System.Object then the layout type attributes need to match
            if ((!type.IsValueType && !type.IsObject) &&
                (type.IsSequentialLayout || type.IsExplicitLayout) &&
                (!type.BaseType.IsValueType && !type.BaseType.IsObject))
            {
                MetadataType baseType = type.BaseType;

                if (type.IsSequentialLayout != baseType.IsSequentialLayout ||
                    type.IsExplicitLayout != baseType.IsExplicitLayout)
                {
                    throw new TypeLoadException();
                }
            }

            // Enum types must have a single instance field
            if (type.IsEnum && numInstanceFields != 1)
            {
                throw new TypeLoadException();
            }

            if (type.IsPrimitive)
            {
                // Primitive types are special - they may have a single field of the same type
                // as the type itself. They do not do the rest of instance field layout.
                if (numInstanceFields > 1)
                {
                    throw new TypeLoadException();
                }

                int byteCount;
                var sizeAndAlignment = ComputeInstanceSize(
                    type,
                    type.Context.Target.GetWellKnownTypeSize(type),
                    type.Context.Target.GetWellKnownTypeAlignment(type),
                    out byteCount
                    );

                ComputedInstanceFieldLayout result = new ComputedInstanceFieldLayout
                {
                    ByteCount = byteCount,
                    FieldAlignment = sizeAndAlignment.Alignment,
                    FieldSize = sizeAndAlignment.Size,
                    PackValue = type.Context.Target.DefaultPackingSize
                };

                if (numInstanceFields > 0)
                {
                    result.Offsets = new FieldAndOffset[] {
                        new FieldAndOffset(type.GetFields().Single(f => !f.IsStatic), 0)
                    };
                }

                return result;
            }

            // Verify that no ByRef types present in this type's fields
            foreach (var field in type.GetFields())
                if (field.FieldType is ByRefType)
                    throw new TypeLoadException();

            // If the type has layout, read its packing and size info
            // If the type has explicit layout, also read the field offset info
            if (type.IsExplicitLayout || type.IsSequentialLayout)
            {
                if (type.IsEnum)
                {
                    throw new TypeLoadException();
                }

                var layoutMetadata = type.GetClassLayout();

                // If packing is out of range or not a power of two, throw that the size is invalid
                int packing = layoutMetadata.PackingSize;
                if (packing < 0 || packing > 128 || ((packing & (packing - 1)) != 0))
                {
                    throw new TypeLoadException();
                }

                Debug.Assert(layoutMetadata.Offsets == null || layoutMetadata.Offsets.Length == numInstanceFields);
            }

            // At this point all special cases are handled and all inputs validated

            InstanceFieldMason mason = new InstanceFieldMason(type, numInstanceFields);

            if (type.IsExplicitLayout)
            {
                mason.ComputeExplicitFieldLayout();
            }
            else
            {
                // Treat auto layout as sequential for now
                mason.ComputeSequentialFieldLayout();
            }

            return mason._computedLayout;
        }

        private void ComputeExplicitFieldLayout()
        {
            // Instance slice size is the total size of instance not including the base type.
            // It is calculated as the field whose offset and size add to the greatest value.
            int cumulativeInstanceFieldPos =
                _type.HasBaseType && !_type.IsValueType ? _type.BaseType.InstanceByteCount : 0;
            int instanceSize = cumulativeInstanceFieldPos;

            var layoutMetadata = _type.GetClassLayout();

            int packingSize = ComputePackingSize(_type);
            int largestAlignmentRequired = 1;

            var offsets = new FieldAndOffset[_numInstanceFields];
            int fieldOrdinal = 0;

            foreach (var fieldAndOffset in layoutMetadata.Offsets)
            {
                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(fieldAndOffset.Field.FieldType, packingSize);

                if (fieldSizeAndAlignment.Alignment > largestAlignmentRequired)
                    largestAlignmentRequired = fieldSizeAndAlignment.Alignment;

                if (fieldAndOffset.Offset == FieldAndOffset.InvalidOffset)
                    throw new TypeLoadException();

                int computedOffset = checked(fieldAndOffset.Offset + cumulativeInstanceFieldPos);

                switch (fieldAndOffset.Field.FieldType.Category)
                {
                    case TypeFlags.Array:
                    case TypeFlags.Class:
                        {
                            int offsetModulo = computedOffset % _type.Context.Target.PointerSize;
                            if (offsetModulo != 0)
                            {
                                // GC pointers MUST be aligned.
                                if (offsetModulo == 4)
                                {
                                    // We must be attempting to compile a 32bit app targeting a 64 bit platform.
                                    throw new TypeLoadException();
                                }
                                else
                                {
                                    // Its just wrong
                                    throw new TypeLoadException();
                                }
                            }
                            break;
                        }
                }

                offsets[fieldOrdinal] = new FieldAndOffset(fieldAndOffset.Field, computedOffset);

                int fieldExtent = checked(computedOffset + fieldSizeAndAlignment.Size);
                if (fieldExtent > instanceSize)
                {
                    instanceSize = fieldExtent;
                }

                fieldOrdinal++;
            }

            if (_type.IsValueType && layoutMetadata.Size > instanceSize)
            {
                instanceSize = layoutMetadata.Size;
            }

            int instanceByteCount;
            var instanceSizeAndAlignment = ComputeInstanceSize(_type, instanceSize, largestAlignmentRequired, out instanceByteCount);

            _computedLayout.FieldAlignment = instanceSizeAndAlignment.Alignment;
            _computedLayout.FieldSize = instanceSizeAndAlignment.Size;
            _computedLayout.ByteCount = instanceByteCount;            
            _computedLayout.Offsets = offsets;
        }

        private void ComputeSequentialFieldLayout()
        {
            var offsets = new FieldAndOffset[_numInstanceFields];

            // For types inheriting from another type, field offsets continue on from where they left off
            int cumulativeInstanceFieldPos = ComputeBytesUsedInParentType(_type);

            int largestAlignmentRequirement = 1;
            int fieldOrdinal = 0;
            int packingSize = ComputePackingSize(_type);

            foreach (var field in _type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                var fieldSizeAndAlignment = ComputeFieldSizeAndAlignment(field.FieldType, packingSize);

                if (fieldSizeAndAlignment.Alignment > largestAlignmentRequirement)
                    largestAlignmentRequirement = fieldSizeAndAlignment.Alignment;

                cumulativeInstanceFieldPos = AlignmentHelper.AlignUp(cumulativeInstanceFieldPos, fieldSizeAndAlignment.Alignment);
                offsets[fieldOrdinal] = new FieldAndOffset(field, cumulativeInstanceFieldPos);
                cumulativeInstanceFieldPos = checked(cumulativeInstanceFieldPos + fieldSizeAndAlignment.Size);

                fieldOrdinal++;
            }

            if (_type.IsValueType)
            {
                var layoutMetadata = _type.GetClassLayout();
                cumulativeInstanceFieldPos = Math.Max(cumulativeInstanceFieldPos, layoutMetadata.Size);
            }

            int instanceByteCount;
            var instanceSizeAndAlignment = ComputeInstanceSize(_type, cumulativeInstanceFieldPos, largestAlignmentRequirement, out instanceByteCount);

            _computedLayout.FieldAlignment = instanceSizeAndAlignment.Alignment;
            _computedLayout.FieldSize = instanceSizeAndAlignment.Size;
            _computedLayout.ByteCount = instanceByteCount;
            _computedLayout.Offsets = offsets;
        }

        private static int ComputeBytesUsedInParentType(MetadataType type)
        {
            int cumulativeInstanceFieldPos = 0;

            if (!type.IsValueType && type.HasBaseType)
            {
                cumulativeInstanceFieldPos = ComputeBytesUsedInParentType(type.BaseType);

                foreach (var field in type.BaseType.GetFields())
                {
                    if (field.IsStatic)
                        continue;

                    // We can pass zero as packing size because we don't care about the alignment part
                    SizeAndAlignment sizeAndAlignment = ComputeFieldSizeAndAlignment(field.FieldType, 0);
                    int fieldEnd = checked(field.Offset + sizeAndAlignment.Size);

                    if (fieldEnd > cumulativeInstanceFieldPos)
                        cumulativeInstanceFieldPos = fieldEnd;
                }
            }

            return cumulativeInstanceFieldPos;
        }

        private static SizeAndAlignment ComputeFieldSizeAndAlignment(TypeDesc fieldType, int packingSize)
        {
            SizeAndAlignment result;

            if (fieldType is MetadataType)
            {
                if (fieldType.IsValueType)
                {
                    MetadataType metadataType = (MetadataType)fieldType;
                    result.Size = metadataType.InstanceFieldSize;
                    result.Alignment = metadataType.InstanceFieldAlignment;
                }
                else
                {
                    result.Size = fieldType.Context.Target.PointerSize;
                    result.Alignment = fieldType.Context.Target.PointerSize;
                }
            }
            else if (fieldType is ByRefType || fieldType is ArrayType)
            {
                result.Size = fieldType.Context.Target.PointerSize;
                result.Alignment = fieldType.Context.Target.PointerSize;
            }
            else if (fieldType is PointerType)
            {
                result.Size = fieldType.Context.Target.PointerSize;
                result.Alignment = fieldType.Context.Target.PointerSize;
            }
            else
                throw new NotImplementedException();

            result.Alignment = Math.Min(result.Alignment, packingSize);

            return result;
        }

        private static int ComputePackingSize(MetadataType type)
        {
            var layoutMetadata = type.GetClassLayout();

            // If a type contains pointers then the metadata specified packing size is ignored (On desktop this is disqualification from ManagedSequential)
            if (layoutMetadata.PackingSize == 0 || type.ContainsPointers)
                return type.Context.Target.DefaultPackingSize;
            else
                return layoutMetadata.PackingSize;
        }

        private static SizeAndAlignment ComputeInstanceSize(MetadataType type, int count, int alignment, out int byteCount)
        {
            SizeAndAlignment result;

            count = AlignmentHelper.AlignUp(count, alignment);

            int targetPointerSize = type.Context.Target.PointerSize;

            // Pad the length of structs to be 1 if they are empty so we have no zero-length structures
            if (type.IsValueType && count == 0)
            {
                count = 1;
            }

            if (type.IsValueType)
            {
                result.Size = count;
                result.Alignment = alignment;
            }
            else
            {
                result.Size = targetPointerSize;
                result.Alignment = targetPointerSize;
            }

            // Size all objects on pointer boundaries because the GC requires it if any fields are object refs
            count = AlignmentHelper.AlignUp(count, targetPointerSize);
            byteCount = count;

            return result;
        }

        private struct SizeAndAlignment
        {
            public int Size;
            public int Alignment;
        }
    }

    public partial class MetadataType
    {
        private class FieldLayoutFlags
        {
            public const int HasContainsPointers = 1;
            public const int ContainsPointers = 2;
            public const int HasInstanceFieldLayout = 4;
        }

        volatile int _fieldLayoutFlags;

        int _instanceFieldSize;
        int _instanceByteCount;
        int _instanceFieldAlignment;

        public bool ContainsPointers
        {
            get
            {
                if ((_fieldLayoutFlags & FieldLayoutFlags.HasContainsPointers) == 0)
                {
                    var flagsToAdd = FieldLayoutFlags.HasContainsPointers;

                    if (ComputeTypeContainsPointers())
                        flagsToAdd |= FieldLayoutFlags.ContainsPointers;

                    EnableFieldLayoutFlags(flagsToAdd);
                }
                return (_fieldLayoutFlags & FieldLayoutFlags.ContainsPointers) != 0;
            }
        }

        public int InstanceFieldSize
        {
            get
            {
                if ((_fieldLayoutFlags & FieldLayoutFlags.HasInstanceFieldLayout) == 0)
                {
                    ComputeInstanceFieldLayout();
                }
                return _instanceFieldSize;
            }
        }

        public int InstanceFieldAlignment
        {
            get
            {
                if ((_fieldLayoutFlags & FieldLayoutFlags.HasInstanceFieldLayout) == 0)
                {
                    ComputeInstanceFieldLayout();
                }
                return _instanceFieldAlignment;
            }
        }

        public int InstanceByteCount
        {
            get
            {
                if ((_fieldLayoutFlags & FieldLayoutFlags.HasInstanceFieldLayout) == 0)
                {
                    ComputeInstanceFieldLayout();
                }
                return _instanceByteCount;
            }
        }

        internal void ComputeInstanceFieldLayout()
        {
            var computedLayout = InstanceFieldMason.ComputeFieldLayout(this);

            _instanceFieldSize = computedLayout.FieldSize;
            _instanceFieldAlignment = computedLayout.FieldAlignment;
            _instanceByteCount = computedLayout.ByteCount;

            foreach (var fieldAndOffset in computedLayout.Offsets)
            {
                Debug.Assert(fieldAndOffset.Field.OwningType == this);
                fieldAndOffset.Field.InitializeOffset(fieldAndOffset.Offset);
            }

            EnableFieldLayoutFlags(FieldLayoutFlags.HasInstanceFieldLayout);
        }

        private bool ComputeTypeContainsPointers()
        {
            if (!IsValueType && HasBaseType && BaseType.ContainsPointers)
                return true;

            foreach (var field in GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                switch (fieldType.Category)
                {
                    // case TypeFlags.SzArray?
                    case TypeFlags.Array:
                    case TypeFlags.Class:
                    // case TypeFlags.MethodGenericParameter?
                    case TypeFlags.GenericParameter:
                    case TypeFlags.ByRef:
                        return true;
                    case TypeFlags.ValueType:
                        if (((MetadataType)fieldType).ContainsPointers)
                            return true;
                        break;
                }
            }

            return false;
        }

        private void EnableFieldLayoutFlags(int flagsToAdd)
        {
            var originalFlags = _fieldLayoutFlags;
            while (Interlocked.CompareExchange(ref _fieldLayoutFlags, originalFlags | flagsToAdd, originalFlags) != originalFlags)
            {
                originalFlags = _fieldLayoutFlags;
            }
        }
    }

    public partial class InstantiatedType
    {
        public override ClassLayoutMetadata GetClassLayout()
        {
            return _typeDef.GetClassLayout();
        }

        public override bool IsExplicitLayout
        {
            get
            {
                return _typeDef.IsExplicitLayout;
            }
        }

        public override bool IsSequentialLayout
        {
            get
            {
                return _typeDef.IsSequentialLayout;
            }
        }

        public override bool IsModuleType { get { return false; } }
    }

    public partial class FieldDesc
    {
        private int _offset = FieldAndOffset.InvalidOffset;

        public int Offset
        {
            get
            {
                if (_offset == FieldAndOffset.InvalidOffset)
                {
                    OwningType.ComputeInstanceFieldLayout();
                }
                return _offset;
            }
        }

        internal void InitializeOffset(int offset)
        {
            Debug.Assert(_offset == FieldAndOffset.InvalidOffset || _offset == offset);
            _offset = offset;
        }
    }

    public struct ComputedInstanceFieldLayout
    {
        public int PackValue;
        public int FieldSize;
        public int FieldAlignment;
        public int ByteCount;
        public FieldAndOffset[] Offsets;
    }
}
