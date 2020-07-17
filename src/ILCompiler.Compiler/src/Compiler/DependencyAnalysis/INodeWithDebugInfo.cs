// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Internal.JitInterface;
using Internal.TypeSystem;

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
        public readonly bool IsParam;
        public readonly TypeDesc Type;
        public List<NativeVarInfo> Ranges;

        public DebugVarInfo(string name, bool isParam, TypeDesc type)
        {
            this.Name = name;
            this.IsParam = isParam;
            this.Type = type;
            this.Ranges = new List<NativeVarInfo>();
        }
    }

    public static class WellKnownLineNumber
    {
        /// <summary>
        /// Informs the debugger that it should step through the annotated sequence point.
        /// </summary>
        public const int DebuggerStepThrough = 0xF00F00;

        /// <summary>
        /// Informs the debugger that it should step into the annotated sequence point.
        /// </summary>
        public const int DebuggerStepIn = 0xFEEFEE;
    }
}
