// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ModuleImportSectionNode : ImportSectionNode
    {
        public List<uint> ModuleCells { get; } = new List<uint>();

        private readonly byte _pointerSize;

        public ModuleImportSectionNode(byte pointerSize)
            : base("ModuleImports", CorCompileImportType.CORCOMPILE_IMPORT_TYPE_UNKNOWN, CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_EAGER, pointerSize, false)
        {
            _pointerSize = pointerSize;
        }

        public int AddModuleCell(ushort assemblyRowid, ushort moduleRowid)
        {
            int index = ModuleCells.Count;
            ModuleCells.Add(assemblyRowid | ((uint)moduleRowid << 16));
            return index;
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            foreach(uint cell in ModuleCells)
            {
                dataBuilder.EmitUInt(cell);
                dataBuilder.EmitZeros(_pointerSize - sizeof(uint));
            }
        }
    }
}
