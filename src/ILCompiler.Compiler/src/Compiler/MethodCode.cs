// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ILCompiler
{
    class MethodCode
    {
        public byte[] Code;
        public byte[] ColdCode;

        public int RODataAlignment;
        public byte[] ROData;

        public Relocation[] Relocs;

        public FrameInfo[] FrameInfos;
        public DebugLocInfo[] DebugLocInfos;
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

    public struct DebugLocInfo
    {
        public int NativeOffset;
        public string FileName;
        public int LineNumber;
        public int ColNumber;

        public DebugLocInfo(int nativeOffset, string fileName, int lineNumber, int colNumber = 0)
        {
            NativeOffset = nativeOffset;
            FileName = fileName;
            LineNumber = lineNumber;
            ColNumber = colNumber;
        }
    }
}
