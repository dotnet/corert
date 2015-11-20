// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // An implementation of the FieldLayout functionality that should be suitable for all types
    // that have Metadata available.

    public abstract partial class MetadataType : DefType
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

        private ThreadSafeFlags _fieldLayoutFlags;

        private int _instanceFieldSize;
        private int _instanceFieldAlignment;
        private int _instanceByteCountUnaligned;
        private int _instanceByteAlignment;

        // Information about various static blocks is rare, so we keep it out of line.
        private StaticBlockInfo _staticBlockInfo;

        public override bool ContainsPointers
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

        public override int InstanceFieldSize
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

        public override int InstanceFieldAlignment
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

        public override int InstanceByteCountUnaligned
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

        public override int InstanceByteAlignment
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

        public override int NonGCStaticFieldSize
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

        public override int NonGCStaticFieldAlignment
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

        public override int GCStaticFieldSize
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

        public override int GCStaticFieldAlignment
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

        public override int ThreadStaticFieldSize
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

        public override int ThreadStaticFieldAlignment
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
