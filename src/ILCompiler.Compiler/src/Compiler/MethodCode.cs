// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ILCompiler
{
    internal class MethodCode
    {
        public byte[] Code;
        public byte[] ColdCode;

        public int RODataAlignment;
        public byte[] ROData;

        public Relocation[] Relocs;

        public FrameInfo[] FrameInfos;
        public DebugLocInfo[] DebugLocInfos;
    }

    internal class BlockRelativeTarget
    {
        public BlockType Block;
        public int Offset;
    }

    /// <summary>
    /// Various type of block.
    /// </summary>
    public enum BlockType : sbyte
    {
        /// <summary>Not a generated block.</summary>
        Unknown = -1,
        /// <summary>Represent code.</summary>
        Code = 0,
        /// <summary>Represent cold code (i.e. code not called frequently).</summary>
        ColdCode = 1,
        /// <summary>Read-only data.</summary>
        ROData = 2
    }

    internal struct Relocation
    {
        public ushort RelocType;
        public BlockType Block;
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
