// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    // This is the api surface necessary to query the field layout of a type
    public abstract partial class DefType : TypeDesc
    {
        private class FieldLayoutFlags
        {
            public const int HasContainsPointers = 1;
            public const int ContainsPointers = 2;
            public const int HasInstanceFieldLayout = 4;
            public const int HasStaticFieldLayout = 8;
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

        /// <summary>
        /// Does a type transitively have any fields which are GC object pointers
        /// </summary>
        public bool ContainsPointers
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasContainsPointers))
                {
                    ComputeTypeContainsPointers();
                }
                return _fieldLayoutFlags.HasFlags(FieldLayoutFlags.ContainsPointers);
            }
        }

        /// <summary>
        /// The number of bytes required to hold a field of this type
        /// </summary>
        public int InstanceFieldSize
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasInstanceFieldLayout))
                {
                    ComputeInstanceFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasInstanceFieldLayout))
                {
                    ComputeInstanceFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasInstanceFieldLayout))
                {
                    ComputeInstanceFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasInstanceFieldLayout))
                {
                    ComputeInstanceFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasStaticFieldLayout))
                {
                    ComputeStaticFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasStaticFieldLayout))
                {
                    ComputeStaticFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasStaticFieldLayout))
                {
                    ComputeStaticFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasStaticFieldLayout))
                {
                    ComputeStaticFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasStaticFieldLayout))
                {
                    ComputeStaticFieldLayout();
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
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasStaticFieldLayout))
                {
                    ComputeStaticFieldLayout();
                }
                return _staticBlockInfo == null ? 0 : _staticBlockInfo.ThreadStatics.LargestAlignment;
            }
        }

        internal void ComputeInstanceFieldLayout()
        {
            var computedLayout = this.Context.GetLayoutAlgorithmForType(this).ComputeInstanceFieldLayout(this);

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
            }

            _fieldLayoutFlags.AddFlags(FieldLayoutFlags.HasInstanceFieldLayout);
        }

        internal void ComputeStaticFieldLayout()
        {
            var computedStaticLayout = this.Context.GetLayoutAlgorithmForType(this).ComputeStaticFieldLayout(this);

            if (computedStaticLayout.Offsets != null)
            {
                Debug.Assert(computedStaticLayout.Offsets.Length > 0);

                var staticBlockInfo = new StaticBlockInfo
                {
                    NonGcStatics = computedStaticLayout.NonGcStatics,
                    GcStatics = computedStaticLayout.GcStatics,
                    ThreadStatics = computedStaticLayout.ThreadStatics
                };
                _staticBlockInfo = staticBlockInfo;

                foreach (var fieldAndOffset in computedStaticLayout.Offsets)
                {
                    Debug.Assert(fieldAndOffset.Field.OwningType == this);
                    fieldAndOffset.Field.InitializeOffset(fieldAndOffset.Offset);
                }
            }

            _fieldLayoutFlags.AddFlags(FieldLayoutFlags.HasStaticFieldLayout);
        }

        private void ComputeTypeContainsPointers()
        {
            int flagsToAdd = FieldLayoutFlags.HasContainsPointers;

            if (!IsValueType && HasBaseType && BaseType.ContainsPointers)
            {
                _fieldLayoutFlags.AddFlags(flagsToAdd | FieldLayoutFlags.ContainsPointers);
                return;
            }

            if (this.Context.GetLayoutAlgorithmForType(this).ComputeContainsPointers(this))
            {
                flagsToAdd |= FieldLayoutFlags.ContainsPointers;
            }

            _fieldLayoutFlags.AddFlags(flagsToAdd);
        }
    }

}