// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILToNative
{
    class MethodCode
    {
        public byte[] Code;
        public byte[] ColdCode;

        public int RODataAlignment;
        public byte[] ROData;

        public Relocation[] Relocs;

        public FrameInfo[] FrameInfos;
    }

    class BlockRelativeTarget
    {
        public sbyte Block;
        public int Offset;
    }

    struct Relocation
    {
        public ushort RelocType;
        public sbyte Block; // Code = 0, ColdCode = 1, ROData = 2
        public int Offset;
        public Object Target;
        public int Delta;
    }

    public class FrameInfo
    {
        public int StartOffset;
        public int EndOffset;
        public byte[] BlobData;
    }
}
