// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    /// <summary>
    /// CorCompileImportSection describes image range with references to other assemblies or runtime data structures
    ///
    /// There is number of different types of these ranges: eagerly initialized at image load vs. lazily initialized at method entry
    /// vs. lazily initialized on first use; hot vs. cold, handles vs. code pointers, etc.
    /// </summary>
    struct CorCompileImportSection
    {
        public readonly int SectionIndex;          // Section containing values to be fixed up
        public readonly ushort Flags;              // One or more of CorCompileImportFlags
        public readonly byte Type;                 // One of CorCompileImportType
        public readonly byte EntrySize;
        public readonly ISymbolNode Signatures;    // RVA of optional signature descriptors
        public readonly ISymbolNode AuxiliaryData; // RVA of optional auxiliary data (typically GC info)

        public CorCompileImportSection(
            int sectionIndex,
            ushort flags,
            byte type,
            byte entrySize,
            ISymbolNode signatures,
            ISymbolNode auxiliaryData)
        {
            SectionIndex = sectionIndex;
            Flags = flags;
            Type = type;
            EntrySize = entrySize;
            Signatures = signatures;
            AuxiliaryData = auxiliaryData;
        }

        public enum CorCompileImportType : byte
        {
            CORCOMPILE_IMPORT_TYPE_UNKNOWN          = 0,
            CORCOMPILE_IMPORT_TYPE_EXTERNAL_METHOD  = 1,
            CORCOMPILE_IMPORT_TYPE_STUB_DISPATCH    = 2,
            CORCOMPILE_IMPORT_TYPE_STRING_HANDLE    = 3,
            CORCOMPILE_IMPORT_TYPE_TYPE_HANDLE      = 4,
            CORCOMPILE_IMPORT_TYPE_METHOD_HANDLE    = 5,
            CORCOMPILE_IMPORT_TYPE_VIRTUAL_METHOD   = 6,
        };
        
        public enum CorCompileImportFlags : ushort
        {
            CORCOMPILE_IMPORT_FLAGS_EAGER           = 0x0001,   // Section at module load time.
            CORCOMPILE_IMPORT_FLAGS_CODE            = 0x0002,   // Section contains code.
            CORCOMPILE_IMPORT_FLAGS_PCODE           = 0x0004,   // Section contains pointers to code.
        };
    };
    
    public class ImportSectionsTableNode : ArrayOfEmbeddedDataNode<ImportSectionNode>
    {   
        public ImportSectionsTableNode(TargetDetails target)
            : base("ImportSectionsTableStart", "ImportSectionsTableEnd", null)
        {
        }
        
        protected override void GetElementDataForNodes(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly)
        {
            builder.RequireInitialPointerAlignment();

            foreach (ImportSectionNode node in NodesList)
            {
                if (!relocsOnly)
                    node.InitializeOffsetFromBeginningOfArray(builder.CountBytes);

                node.EncodeData(ref builder, factory, relocsOnly);
            }
        }

        protected override int ClassCode => 787556329;
    }
}
