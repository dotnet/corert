// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.SymbolReader
{
    /// <summary>
    ///  IL sequence point record
    /// </summary>
    public struct ILSequencePoint
    {
        public readonly int Offset;
        public readonly string Document;
        public readonly int LineNumber;
        // TODO: The remaining info

        public ILSequencePoint(int offset, string document, int lineNumber)
        {
            Offset = offset;
            Document = document;
            LineNumber = lineNumber;
        }
    }

    /// <summary>
    ///  IL local variable debug record
    /// </summary>
    public struct ILLocalVariable
    {
        public readonly int Slot;
        public readonly string Name;
        public readonly bool CompilerGenerated;

        public ILLocalVariable(int slot, string name, bool compilerGenerated)
        {
            Slot = slot;
            Name = name;
            CompilerGenerated = compilerGenerated;
        }
    }

    /// <summary>
    /// Abstraction for reading Pdb files
    /// </summary>
    public abstract class PdbSymbolReader : IDisposable
    {
        public abstract IEnumerable<ILSequencePoint> GetSequencePointsForMethod(int methodToken);
        public abstract IEnumerable<ILLocalVariable> GetLocalVariableNamesForMethod(int methodToken);
        public abstract void Dispose();
    }
}
