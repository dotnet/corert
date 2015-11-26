// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ILCompiler.DependencyAnalysis
{
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

    public interface INodeWithDebugInfo
    {
        DebugLocInfo[] DebugLocInfos
        {
            get;
        }
    }
}
