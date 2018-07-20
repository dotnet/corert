// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;

using Internal.JitInterface;
using Internal.NativeFormat;
using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class MethodEntryPointTableNode : HeaderTableNode
    {
        private struct EntryPoint
        {
            public static EntryPoint Null = new EntryPoint(-1, null);

            public readonly int MethodIndex;
            public readonly ObjectNode Method;

            public bool IsNull => (MethodIndex < 0);
            
            public EntryPoint(int methodIndex, ObjectNode method)
            {
                MethodIndex = methodIndex;
                Method = method;
            }
        }

        /// <summary>
        /// This helper structure represents the "coordinates" of a single
        /// indirection cell in the import tables (index of the import
        /// section table and offset within the table).
        /// </summary>
        private struct FixupCell
        {
            public static readonly IComparer<FixupCell> Comparer = new CellComparer();

            public int TableIndex;
            public int ImportOffset;

            public FixupCell(int tableIndex, int importOffset)
            {
                TableIndex = tableIndex;
                ImportOffset = importOffset;
            }

            private class CellComparer : IComparer<FixupCell>
            {
                public int Compare(FixupCell a, FixupCell b)
                {
                    int result = a.TableIndex.CompareTo(b.TableIndex);
                    if (result == 0)
                    {
                        result = a.ImportOffset.CompareTo(b.ImportOffset);
                    }
                    return result;
                }
            }
        }

        List<EntryPoint> _ridToEntryPoint;

        public MethodEntryPointTableNode(TargetDetails target)
            : base(target)
        {
            _ridToEntryPoint = new List<EntryPoint>();
        }
        
        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunMethodEntryPointTable");
        }

        public void Add(MethodWithGCInfo methodNode, int methodIndex, NodeFactory factory)
        {
            uint rid;
            if (methodNode.Method is EcmaMethod ecmaMethod)
            {
                // Strip away the token type bits, keep just the low 24 bits RID
                rid = SignatureBuilder.RidFromToken((mdToken)MetadataTokens.GetToken(ecmaMethod.Handle));
            }
            else if (methodNode.Method is MethodForInstantiatedType methodOnInstantiatedType)
            {
                if (methodOnInstantiatedType.GetTypicalMethodDefinition() is EcmaMethod ecmaTypicalMethod)
                {
                    rid = SignatureBuilder.RidFromToken((mdToken)MetadataTokens.GetToken(ecmaTypicalMethod.Handle));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            Debug.Assert(rid != 0);
            rid--;

            while (_ridToEntryPoint.Count <= rid)
            {
                _ridToEntryPoint.Add(EntryPoint.Null);
            }

            _ridToEntryPoint[(int)rid] = new EntryPoint(methodIndex, methodNode);
        }

        private byte[] GetFixupBlob(NodeFactory factory, ObjectNode node)
        {
            Relocation[] relocations = node.GetData(factory, relocsOnly: true).Relocs;

            if (relocations == null)
            {
                return null;
            }

            List<FixupCell> fixupCells = null;

            foreach (Relocation reloc in relocations)
            {
                if (reloc.Target is Import fixupCell && fixupCell.EmitPrecode)
                {
                    if (fixupCells == null)
                    {
                        fixupCells = new List<FixupCell>();
                    }
                    fixupCells.Add(new FixupCell(fixupCell.Table.IndexFromBeginningOfArray, fixupCell.OffsetFromBeginningOfArray));
                }
            }

            if (fixupCells == null)
            {
                return null;
            }

            fixupCells.Sort(FixupCell.Comparer);

            NibbleWriter writer = new NibbleWriter();

            int curTableIndex = -1;
            int curOffset = 0;

            foreach (FixupCell cell in fixupCells)
            {
                Debug.Assert(cell.ImportOffset % factory.Target.PointerSize == 0);
                int offset = cell.ImportOffset / factory.Target.PointerSize;

                if (cell.TableIndex != curTableIndex)
                {
                    // Write delta relative to the previous table index
                    Debug.Assert(cell.TableIndex > curTableIndex);
                    if (curTableIndex != -1)
                    {
                        writer.WriteUInt(0); // table separator, so add except for the first entry
                        writer.WriteUInt((uint)(cell.TableIndex - curTableIndex)); // add table index delta
                    }
                    else
                    {
                        writer.WriteUInt((uint)cell.TableIndex);
                    }
                    curTableIndex = cell.TableIndex;

                    // This is the first fixup in the current table.
                    // We will write it out completely (without delta-encoding)
                    writer.WriteUInt((uint)offset);
                }
                else if (offset != curOffset) // ignore duplicate fixup cells
                {
                    // This is not the first entry in the current table.
                    // We will write out the delta relative to the previous fixup value
                    int delta = offset - curOffset;
                    Debug.Assert(delta > 0);
                    writer.WriteUInt((uint)delta);
                }

                // future entries for this table would be relative to this rva
                curOffset = offset;
            }

            writer.WriteUInt(0); // table separator
            writer.WriteUInt(0); // fixup list ends

            return writer.ToArray();
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if (relocsOnly)
            {
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, Array.Empty<ISymbolDefinitionNode>());
            }

            NativeWriter writer = new NativeWriter();

            Section arraySection = writer.NewSection();
            VertexArray vertexArray = new VertexArray(arraySection);
            arraySection.Place(vertexArray);

            Section fixupSection = writer.NewSection();

            Dictionary<byte[], BlobVertex> uniqueFixups = new Dictionary<byte[], BlobVertex>(ByteArrayComparer.Instance);

            for (int rid = 0; rid < _ridToEntryPoint.Count; rid++)
            {
                EntryPoint entryPoint = _ridToEntryPoint[rid];
                if (!entryPoint.IsNull)
                {
                    byte[] fixups = GetFixupBlob(factory, entryPoint.Method);

                    BlobVertex fixupBlobVertex = null;
                    if (fixups != null && !uniqueFixups.TryGetValue(fixups, out fixupBlobVertex))
                    {
                        fixupBlobVertex = new BlobVertex(fixups);
                        fixupSection.Place(fixupBlobVertex);
                        uniqueFixups.Add(fixups, fixupBlobVertex);
                    }
                    EntryPointVertex entryPointVertex = new EntryPointVertex((uint)entryPoint.MethodIndex, fixupBlobVertex);
                    vertexArray.Set(rid, entryPointVertex);
                }
            }

            vertexArray.ExpandLayout();

            MemoryStream arrayContent = new MemoryStream();
            writer.Save(arrayContent);
            return new ObjectData(
                data: arrayContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        protected override int ClassCode => 787556329;
    }
}
