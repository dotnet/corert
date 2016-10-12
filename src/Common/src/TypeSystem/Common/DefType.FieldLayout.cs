// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    // This is the api surface necessary to query the field layout of a type
    public abstract partial class DefType : TypeDesc
    {
        /// <summary>
        /// Bit flags for layout 
        /// </summary>
        private class FieldLayoutFlags
        {
            /// <summary>
            /// True if ContainsGCPointers has been computed
            /// </summary>
            public const int ComputedContainsGCPointers = 1;

            /// <summary>
            /// True if the type contains GC pointers
            /// </summary>
            public const int ContainsGCPointers = 2;

            /// <summary>
            /// True if the instance type only layout is computed
            /// </summary>
            public const int ComputedInstanceTypeLayout = 4;

            /// <summary>
            /// True if the static field layout for the static regions have been computed
            /// </summary>
            public const int ComputedStaticRegionLayout = 8;

            /// <summary>
            /// True if the instance type layout is complete including fields
            /// </summary>
            public const int ComputedInstanceTypeFieldsLayout = 0x10;

            /// <summary>
            /// True if the static field layout for the static fields have been computed
            /// </summary>
            public const int ComputedStaticFieldsLayout = 0x20;

            /// <summary>
            /// True if information about the shape of value type has been computed.
            /// </summary>
            public const int ComputedValueTypeShapeCharacteristics = 0x40;
        }

        private class StaticBlockInfo
        {
            public StaticsBlock NonGcStatics;
            public StaticsBlock GcStatics;
            public StaticsBlock ThreadStatics;
        }

        ThreadSafeFlags _fieldLayoutFlags;

        int _instanceFieldSize;
        int _instanceFieldAlignment;
        int _instanceByteCountUnaligned;
        int _instanceByteAlignment;

        // Information about various static blocks is rare, so we keep it out of line.
        StaticBlockInfo _staticBlockInfo;

        ValueTypeShapeCharacteristics _valueTypeShapeCharacteristics;

        /// <summary>
        /// Does a type transitively have any fields which are GC object pointers
        /// </summary>
        public bool ContainsGCPointers
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedContainsGCPointers))
                {
                    ComputeTypeContainsGCPointers();
                }
                return _fieldLayoutFlags.HasFlags(FieldLayoutFlags.ContainsGCPointers);
            }
        }

        /// <summary>
        /// The number of bytes required to hold a field of this type
        /// </summary>
        public int InstanceFieldSize
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedInstanceTypeLayout))
                {
                    ComputeInstanceLayout(InstanceLayoutKind.TypeOnly);
                }
                return _instanceFieldSize;
            }
        }

        /// <summary>
        /// What is the alignment requirement of the fields of this type
        /// </summary>
        public int InstanceFieldAlignment
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedInstanceTypeLayout))
                {
                    ComputeInstanceLayout(InstanceLayoutKind.TypeOnly);
                }
                return _instanceFieldAlignment;
            }
        }

        /// <summary>
        /// The number of bytes required when allocating this type on this GC heap
        /// </summary>
        public int InstanceByteCount
        {
            get
            {
                return AlignmentHelper.AlignUp(InstanceByteCountUnaligned, InstanceByteAlignment);
            }
        }

        /// <summary>
        /// The number of bytes used by the instance fields of this type and its parent types without padding at the end for alignment/gc.
        /// </summary>
        public int InstanceByteCountUnaligned
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedInstanceTypeLayout))
                {
                    ComputeInstanceLayout(InstanceLayoutKind.TypeOnly);
                }
                return _instanceByteCountUnaligned;
            }
        }

        /// <summary>
        /// The alignment required for instances of this type on the GC heap
        /// </summary>
        public int InstanceByteAlignment
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedInstanceTypeLayout))
                {
                    ComputeInstanceLayout(InstanceLayoutKind.TypeOnly);
                }
                return _instanceByteAlignment;
            }
        }

        /// <summary>
        /// How many bytes must be allocated to represent the non GC visible static fields of this type.
        /// </summary>
        public int NonGCStaticFieldSize
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticRegionLayout))
                {
                    ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizes);
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.NonGcStatics.Size;
            }
        }

        /// <summary>
        /// What is the alignment required for allocating the non GC visible static fields of this type.
        /// </summary>
        public int NonGCStaticFieldAlignment
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticRegionLayout))
                {
                    ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizes);
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.NonGcStatics.LargestAlignment;
            }
        }

        /// <summary>
        /// How many bytes must be allocated to represent the GC visible static fields of this type.
        /// </summary>
        public int GCStaticFieldSize
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticRegionLayout))
                {
                    ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizes);
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.GcStatics.Size;
            }
        }

        /// <summary>
        /// What is the alignment required for allocating the GC visible static fields of this type.
        /// </summary>
        public int GCStaticFieldAlignment
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticRegionLayout))
                {
                    ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizes);
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.GcStatics.LargestAlignment;
            }
        }

        /// <summary>
        /// How many bytes must be allocated to represent the (potentially GC visible) thread static
        /// fields of this type.
        /// </summary>
        public int ThreadStaticFieldSize
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticRegionLayout))
                {
                    ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizes);
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.ThreadStatics.Size;
            }
        }

        /// <summary>
        /// What is the alignment required for allocating the (potentially GC visible) thread static
        /// fields of this type.
        /// </summary>
        public int ThreadStaticFieldAlignment
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticRegionLayout))
                {
                    ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizes);
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.ThreadStatics.LargestAlignment;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the fields of the type satisfy the Homogeneous Float Aggregate classification.
        /// </summary>
        public bool IsHfa
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedValueTypeShapeCharacteristics))
                {
                    ComputeValueTypeShapeCharacteristics();
                }
                return (_valueTypeShapeCharacteristics & ValueTypeShapeCharacteristics.HomogenousFloatAggregate) != 0;
            }
        }

        /// <summary>
        /// Get the Homogeneous Float Aggregate element type if this is a HFA type (<see cref="IsHfa"/> is true).
        /// </summary>
        public DefType HfaElementType
        {
            get
            {
                // We are not caching this because this is rare and not worth wasting space in DefType.
                return this.Context.GetLayoutAlgorithmForType(this).ComputeHomogeneousFloatAggregateElementType(this);
            }
        }

        private void ComputeValueTypeShapeCharacteristics()
        {
            _valueTypeShapeCharacteristics = this.Context.GetLayoutAlgorithmForType(this).ComputeValueTypeShapeCharacteristics(this);
            _fieldLayoutFlags.AddFlags(FieldLayoutFlags.ComputedValueTypeShapeCharacteristics);
        }


        internal void ComputeInstanceLayout(InstanceLayoutKind layoutKind)
        {
            if (_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedInstanceTypeFieldsLayout | FieldLayoutFlags.ComputedInstanceTypeLayout))
                return;

            var computedLayout = this.Context.GetLayoutAlgorithmForType(this).ComputeInstanceLayout(this, layoutKind);

            _instanceFieldSize = computedLayout.FieldSize;
            _instanceFieldAlignment = computedLayout.FieldAlignment;
            _instanceByteCountUnaligned = computedLayout.ByteCountUnaligned;
            _instanceByteAlignment = computedLayout.ByteCountAlignment;

            if (computedLayout.Offsets != null)
            {
                foreach (var fieldAndOffset in computedLayout.Offsets)
                {
                    Debug.Assert(fieldAndOffset.Field.OwningType == this);
                    fieldAndOffset.Field.InitializeOffset(fieldAndOffset.Offset);
                }
                _fieldLayoutFlags.AddFlags(FieldLayoutFlags.ComputedInstanceTypeFieldsLayout);
            }

            _fieldLayoutFlags.AddFlags(FieldLayoutFlags.ComputedInstanceTypeLayout);
        }

        internal void ComputeStaticFieldLayout(StaticLayoutKind layoutKind)
        {
            if (_fieldLayoutFlags.HasFlags(FieldLayoutFlags.ComputedStaticFieldsLayout | FieldLayoutFlags.ComputedStaticRegionLayout))
                return;

            var computedStaticLayout = this.Context.GetLayoutAlgorithmForType(this).ComputeStaticFieldLayout(this, layoutKind);

            if ((computedStaticLayout.NonGcStatics.Size != 0) ||
                (computedStaticLayout.GcStatics.Size != 0) ||
                (computedStaticLayout.ThreadStatics.Size != 0))
            {
                var staticBlockInfo = new StaticBlockInfo
                {
                    NonGcStatics = computedStaticLayout.NonGcStatics,
                    GcStatics = computedStaticLayout.GcStatics,
                    ThreadStatics = computedStaticLayout.ThreadStatics
                };
                _staticBlockInfo = staticBlockInfo;
            }

            if (computedStaticLayout.Offsets != null)
            {
                foreach (var fieldAndOffset in computedStaticLayout.Offsets)
                {
                    Debug.Assert(fieldAndOffset.Field.OwningType == this);
                    fieldAndOffset.Field.InitializeOffset(fieldAndOffset.Offset);
                }
                _fieldLayoutFlags.AddFlags(FieldLayoutFlags.ComputedStaticFieldsLayout);
            }

            _fieldLayoutFlags.AddFlags(FieldLayoutFlags.ComputedStaticRegionLayout);
        }

        private void ComputeTypeContainsGCPointers()
        {
            int flagsToAdd = FieldLayoutFlags.ComputedContainsGCPointers;

            if (!IsValueType && HasBaseType && BaseType.ContainsGCPointers)
            {
                _fieldLayoutFlags.AddFlags(flagsToAdd | FieldLayoutFlags.ContainsGCPointers);
                return;
            }

            if (this.Context.GetLayoutAlgorithmForType(this).ComputeContainsGCPointers(this))
            {
                flagsToAdd |= FieldLayoutFlags.ContainsGCPointers;
            }

            _fieldLayoutFlags.AddFlags(flagsToAdd);
        }
    }

}
