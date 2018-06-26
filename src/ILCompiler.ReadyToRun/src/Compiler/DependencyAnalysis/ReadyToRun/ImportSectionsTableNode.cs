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
    
    public class ImportSectionsTableNode : HeaderTableNode
    {
        List<CorCompileImportSection> _importSections;
        
        public ImportSectionsTableNode(TargetDetails target)
            : base(target)
        {
            _importSections = new List<CorCompileImportSection>();
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__CorCompileImportSections");
        }

        public void Add(int sectionIndex, ushort flags, byte type, byte entrySize, ISymbolNode signatures, ISymbolNode auxiliaryData)
        {
            _importSections.Add(new CorCompileImportSection(sectionIndex, flags, type, entrySize, signatures, auxiliaryData));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder();
            dataBuilder.AddSymbol(this);
            
            foreach (CorCompileImportSection section in _importSections)
            {
                // TODO: resolve the appropriate section range
                // This will require putting the import section to a late PE section
                // to make sure all the interesting sections have been placed beforehand.
                // Alternatively we'd have to implement a new type of section-relative selocations.
                DirectoryEntry sectionRange = new DirectoryEntry();
                dataBuilder.EmitInt(sectionRange.RelativeVirtualAddress);
                dataBuilder.EmitInt(sectionRange.Size);
                dataBuilder.EmitShort(unchecked((short)section.Flags));
                dataBuilder.EmitByte(section.Type);
                dataBuilder.EmitByte(section.EntrySize);
                if (section.Signatures != null)
                {
                    dataBuilder.EmitReloc(section.Signatures, RelocType.IMAGE_REL_BASED_ADDR32NB, 0);
                }
                else
                {
                    dataBuilder.EmitUInt(0);
                }
                if (section.AuxiliaryData != null)
                {
                    dataBuilder.EmitReloc(section.AuxiliaryData, RelocType.IMAGE_REL_BASED_ADDR32NB, 0);
                }
                else
                {
                    dataBuilder.EmitUInt(0);
                }
            }
            
            return dataBuilder.ToObjectData();
        }

        protected override int ClassCode => 787556329;
    }
}
