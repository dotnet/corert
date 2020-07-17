// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a symbol definition with debug info, i.e., the S_GDATA32 record.
    /// </summary>
    public interface ISymbolNodeWithDebugInfo : ISymbolDefinitionNode
    {
        IDebugInfo DebugInfo { get; }
    }

    public interface IDebugInfo
    { }

    public interface ITypeIndexDebugInfo : IDebugInfo
    {
        int TypeIndex { get; }
    }

    public class NullTypeIndexDebugInfo : ITypeIndexDebugInfo
    {
        private NullTypeIndexDebugInfo() { }

        public int TypeIndex => 0;

        public static IDebugInfo Instance
        {
            get { return new NullTypeIndexDebugInfo(); }
        }
    }
}
