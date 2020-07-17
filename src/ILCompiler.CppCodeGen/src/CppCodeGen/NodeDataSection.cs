// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.Compiler.CppCodeGen
{
    internal enum NodeDataSectionType
    {
        Relocation,
        ByteData
    }

    internal struct NodeDataSection
    {
        public readonly NodeDataSectionType SectionType;
        public readonly int SectionSize;

        public NodeDataSection(NodeDataSectionType sectionType, int sectionSize)
        {
            SectionType = sectionType;
            SectionSize = sectionSize;
        }
    }
}
