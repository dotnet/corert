// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    public class FrameInfo
    {
        public int StartOffset;
        public int EndOffset;
        public byte[] BlobData;
    }

    public interface INodeWithFrameInfo
    {
        FrameInfo[] FrameInfos
        {
            get;
        }
    }
}
