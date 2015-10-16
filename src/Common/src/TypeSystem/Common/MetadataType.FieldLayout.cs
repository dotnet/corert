// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // An implementation of the FieldLayout functionality that should be suitable for all types
    // that have Metadata available.

    public partial class MetadataType
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
        int _instanceByteCount;
        int _instanceFieldAlignment;

        // Information about various static blocks is rare, so we keep it out of line.
        StaticBlockInfo _staticBlockInfo;

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

        public override int InstanceByteCount
        {
            get
            {
                if (!_fieldLayoutFlags.HasFlags(FieldLayoutFlags.HasInstanceFieldLayout))
                {
                    ComputeInstanceFieldLayout();
                }
                return _instanceByteCount;
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

        internal void ComputeInstanceFieldLayout()
        {
            var computedLayout = FieldLayoutAlgorithm.ComputeInstanceFieldLayout(this);

            _instanceFieldSize = computedLayout.FieldSize;
            _instanceFieldAlignment = computedLayout.FieldAlignment;
            _instanceByteCount = computedLayout.ByteCount;

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
            var computedStaticLayout = FieldLayoutAlgorithm.ComputeStaticFieldLayout(this);

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

            foreach (var field in GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsValueType)
                {
                    if (fieldType.IsPrimitive)
                        continue;

                    if (((MetadataType)fieldType).ContainsPointers)
                    {
                        flagsToAdd |= FieldLayoutFlags.ContainsPointers;
                        break;
                    }
                }
                else if (fieldType is DefType || fieldType is ArrayType || fieldType.IsByRef)
                {
                    flagsToAdd |= FieldLayoutFlags.ContainsPointers;
                    break;
                }
            }

            _fieldLayoutFlags.AddFlags(flagsToAdd);
        }
    }
}