// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Internal.JitInterface;

namespace ILCompiler.DependencyAnalysis
{
    public struct DebugLocInfo
    {
        public readonly int NativeOffset;
        public readonly string FileName;
        public readonly int LineNumber;
        public readonly int ColNumber;

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

        DebugVarInfo[] DebugVarInfos
        {
            get;
        }
    }
     
    public struct DebugVarInfo
    {
        public readonly string Name;
        public readonly uint TypeIndex;
        public readonly bool IsParam;
        public List<NativeVarInfo> Ranges;

        public DebugVarInfo(string name, bool isParam, uint typeIndex)
        {
            this.Name = name;
            this.TypeIndex = typeIndex;
            this.IsParam = isParam;
            this.Ranges = new List<NativeVarInfo>();
        }
    }
}
