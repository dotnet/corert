// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Internal.TypeSystem
{
    public abstract partial class MetadataType : TypeDesc
    {
        public abstract ClassLayoutMetadata GetClassLayout();

        public abstract bool IsExplicitLayout { get; }

        public abstract bool IsSequentialLayout { get; }

        public abstract bool IsModuleType { get; }
    }

    public struct ClassLayoutMetadata
    {
        public int PackingSize;
        public int Size;
        public FieldAndOffset[] Offsets;
    }

    public struct FieldAndOffset
    {
        public const int InvalidOffset = -1;

        public readonly FieldDesc Field;
        public readonly int Offset;
        public FieldAndOffset(FieldDesc field, int offset)
        {
            Field = field;
            Offset = offset;
        }
    }
}
