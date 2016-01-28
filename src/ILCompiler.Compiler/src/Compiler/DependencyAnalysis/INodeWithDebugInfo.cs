// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
