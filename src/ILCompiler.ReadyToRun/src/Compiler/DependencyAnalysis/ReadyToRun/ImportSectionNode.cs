// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ImportSectionNode : EmbeddedObjectNode
    {
        private readonly ArrayOfEmbeddedDataNode<Import> _imports;
        private readonly ArrayOfEmbeddedPointersNode<Signature> _signatures;
        private readonly CorCompileImportType _type;
        private readonly CorCompileImportFlags _flags;
        private readonly byte _entrySize;

        public ImportSectionNode(CorCompileImportType importType, CorCompileImportFlags flags, byte entrySize)
        {
            _type = importType;
            _flags = flags;
            _entrySize = entrySize;

            _imports = new ArrayOfEmbeddedDataNode<Import>($"imports_{NodeIdentifier}_start", $"imports_{NodeIdentifier}_end", null);
            _signatures = new ArrayOfEmbeddedPointersNode<Signature>($"signaures_{NodeIdentifier}_start", $"signatures_{NodeIdentifier}_end", null);
        }
        
        private string NodeIdentifier => $"_{_type}_{_flags}_{_entrySize}";

        public void AddImport(ReadyToRunCodegenNodeFactory factory, Import import)
        {
            _imports.AddEmbeddedObject(import);
            _signatures.AddEmbeddedObject(factory.SignatureIndirection(import.GetSignature(factory)));
        }

        public override bool StaticDependenciesAreComputed => true;

        protected override int ClassCode => -62839441;

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            dataBuilder.EmitReloc(_imports.StartSymbol, RelocType.IMAGE_REL_BASED_ADDR32NB, 0);
            if (!relocsOnly)
                dataBuilder.EmitInt(_imports.GetData(factory, false).Data.Length);

            dataBuilder.EmitShort((short)_flags);
            dataBuilder.EmitByte((byte)_type);
            dataBuilder.EmitByte(_entrySize);
            if (!_signatures.ShouldSkipEmittingObjectNode(factory))
            {
                dataBuilder.EmitReloc(_signatures.StartSymbol, RelocType.IMAGE_REL_BASED_ADDR32NB, 0);
            }
            else
            {
                dataBuilder.EmitUInt(0);
            }
            
            dataBuilder.EmitUInt(0);
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            yield return new DependencyListEntry(_imports, "Import section fixup data");
            yield return new DependencyListEntry(_signatures, "Import section signatures");
        }

        protected override string GetName(NodeFactory context)
        {
            return $"ImportSectionNode_{NodeIdentifier}";
        }
    }
}
