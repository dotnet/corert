// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    public struct FrameInfo
    {
        public readonly int StartOffset;
        public readonly int EndOffset;
        public readonly byte[] BlobData;

        public FrameInfo(int startOffset, int endOffset, byte[] blobData)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            BlobData = blobData;
        }
    }

    public interface INodeWithCodeInfo
    {
        FrameInfo[] FrameInfos
        {
            get;
        }

        ObjectNode.ObjectData EHInfo
        {
            get;
        }
    }
}
