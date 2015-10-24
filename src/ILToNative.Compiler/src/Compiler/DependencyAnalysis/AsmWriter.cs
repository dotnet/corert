// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ILToNative.DependencyAnalysisFramework;
using System.Diagnostics;

namespace ILToNative.DependencyAnalysis
{
    /// <summary>
    /// Temporary assembly writer for use until direct obj writer is implemented.
    /// </summary>
    static class AsmWriter
    {
        private static int FindNextRelocOffset(int currentRelocIndex, Relocation[] relocs, byte[] bytes)
        {
            if ((relocs != null) && (currentRelocIndex < relocs.Length))
            {
                int nextRelocOffset = relocs[currentRelocIndex].Offset;
                if (relocs[currentRelocIndex].RelocType == RelocType.IMAGE_REL_BASED_REL32)
                {
                    if (relocs[currentRelocIndex].InstructionLength == 1)
                    {
                        nextRelocOffset--;
                        Debug.Assert((bytes[nextRelocOffset] == 0xE9) || // jmp
                                     (bytes[nextRelocOffset] == 0xE8) || // call
                                     (bytes[nextRelocOffset] == 0x05) || // add
                                     (bytes[nextRelocOffset] == 0x15));  // adc
                    }
                    else if (relocs[currentRelocIndex].InstructionLength == 3)
                    {
                        // TODO make this more structured, and flexible to handle other instructions
                        nextRelocOffset -= 3;
                        Debug.Assert((bytes[nextRelocOffset] == 0x48) || (bytes[nextRelocOffset] == 0x44) || (bytes[nextRelocOffset] == 0x4c)); // REX
                        Debug.Assert(bytes[nextRelocOffset + 1] == 0x8D); // lea opcode
                        byte regTargetByte = bytes[nextRelocOffset + 2];
                        Debug.Assert((regTargetByte & 0x07) == 5);
                        Debug.Assert((regTargetByte & 0xC0) == 0);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                return nextRelocOffset;
            }
            else
            {
                return -1;
            }
        }

        private static void EmitSymbolDefinition(TextWriter output, int currentOffset, ISymbolNode[] definedSymbols, ref int lineLength)
        {
            foreach (ISymbolNode node in definedSymbols)
            {
                if (node.Offset == currentOffset)
                {
                    if (lineLength > 0)
                    {
                        output.WriteLine();
                        lineLength = 0;
                    }

                    output.Write(".global ");
                    output.WriteLine(node.MangledName);

                    output.Write(node.MangledName);
                    output.WriteLine(":");
                }
            }
        }

        public static void EmitAsm(TextWriter Out, IEnumerable<DependencyNode> nodes, NodeFactory factory)
        {
            string currentSection = "";

            foreach (DependencyNode depNode in nodes)
            {
                ObjectNode node = depNode as ObjectNode;
                if (node == null)
                    continue;

                if (node.ShouldSkipEmittingObjectNode(factory))
                    continue;

                ObjectNode.ObjectData nodeContents = node.GetData(factory);

                if (currentSection != node.Section)
                {
                    Out.WriteLine("." + node.Section);
                    currentSection = node.Section;
                }

                Out.WriteLine(".align " + nodeContents.Alignment);

                int lineLength = 0;
                int currentRelocIndex = 0;
                Relocation[] relocs = nodeContents.Relocs;
                int nextRelocOffset = FindNextRelocOffset(currentRelocIndex, relocs, nodeContents.Data);

                for (int i = 0; i < nodeContents.Data.Length; i++)
                {
                    // Emit symbol definitions if necessary
                    EmitSymbolDefinition(Out, i, nodeContents.DefinedSymbols, ref lineLength);

                    if (i == nextRelocOffset)
                    {
                        if (lineLength > 0)
                        {
                            Out.WriteLine();
                            lineLength = 0;
                        }

                        Relocation reloc = relocs[currentRelocIndex];

                        ISymbolNode target = reloc.Target;
                        string targetName = target.MangledName;

                        switch (reloc.RelocType)
                        {
                            // REVIEW: I believe the JIT is emitting 0x3 instead of 0xA
                            // for x64, because emitter from x86 is ported for RyuJIT.
                            // I will consult with Bruce and if he agrees, I will delete
                            // this "case" duplicated by IMAGE_REL_BASED_DIR64.
                            case (RelocType)0x03: // IMAGE_REL_BASED_HIGHLOW
                            case RelocType.IMAGE_REL_BASED_DIR64:
                                Out.Write(".quad ");
                                Out.WriteLine(targetName);
                                i += 7;
                                break;
                            case RelocType.IMAGE_REL_BASED_REL32:
                                if (reloc.InstructionLength == 1)
                                {
                                    switch (nodeContents.Data[i])
                                    {
                                        case 0xE8: // call
                                            Out.Write("call ");
                                            Out.WriteLine(targetName);
                                            break;
                                        case 0xE9: // jmp
                                            Out.Write("jmp ");
                                            Out.WriteLine(targetName);
                                            break;
                                        case 0x05: // add rAX, imm16/32
                                            Out.Write("add ");
                                            Out.Write(targetName);
                                            Out.WriteLine("(%rip), %rax");
                                            break;
                                        case 0x15: // adc rAX, imm16/32
                                            Out.Write("adc ");
                                            Out.Write(targetName);
                                            Out.WriteLine("(%rip), %rax");
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }
                                }
                                else if (reloc.InstructionLength == 3)
                                {
                                    int registerHighBit = nodeContents.Data[i] == 0x48 ? 0 : 8;
                                    int registerOtherBits = (nodeContents.Data[i + 2] & 0x38) >> 3;
                                    X64.Register reg = (X64.Register)(registerHighBit | registerOtherBits);

                                    Out.Write("leaq ");
                                    Out.Write(targetName);
                                    Out.Write("(%rip), %");
                                    Out.WriteLine(reg.ToString().ToLowerInvariant());
                                    i += 2;
                                }

                                i += 4;
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        currentRelocIndex++;
                        nextRelocOffset = FindNextRelocOffset(currentRelocIndex, relocs, nodeContents.Data);
                        continue;
                    }

                    if (lineLength == 0)
                    {
                        Out.Write(".byte ");
                    }
                    else
                    {
                        Out.Write(",");
                    }

                    Out.Write(nodeContents.Data[i]);

                    if (lineLength++ > 15)
                    {
                        Out.WriteLine();
                        lineLength = 0;
                    }
                }

                // It is possible to have a symbol just after all of the data.
                EmitSymbolDefinition(Out, nodeContents.Data.Length, nodeContents.DefinedSymbols, ref lineLength);

                if (lineLength > 0)
                    Out.WriteLine();
                Out.WriteLine();
                Out.Flush();
            }
        }
    }
}
