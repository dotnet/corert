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
using System.Runtime.InteropServices;

namespace ILToNative.DependencyAnalysis
{
    /// <summary>
    /// Object writer using https://github.com/dotnet/llilc
    /// </summary>
    class ObjectWriter : IDisposable
    {
        public const string MainEntryNodeName = "__managed__Main";
        const string NativeObjectWriterFileName = "objwriter";
        [DllImport(NativeObjectWriterFileName)]
        static extern IntPtr InitObjWriter(string objectFilePath);

        [DllImport(NativeObjectWriterFileName)]
        static extern void FinishObjWriter(IntPtr objWriter);

        [DllImport(NativeObjectWriterFileName)]
        static extern void SwitchSection(IntPtr objWriter, string sectionName);
        public void SwitchSection(string sectionName)
        {
            SwitchSection(_nativeObjectWriter, sectionName);
        }

        [DllImport(NativeObjectWriterFileName)]
        static extern void EmitAlignment(IntPtr objWriter, int byteAlignment);
        public void EmitAlignment(int byteAlignment)
        {
            EmitAlignment(_nativeObjectWriter, byteAlignment);
        }

        [DllImport(NativeObjectWriterFileName)]
        static extern void EmitBlob(IntPtr objWriter, int blobSize, byte[] blob);
        public void EmitBlob(int blobSize, byte[] blob)
        {
            EmitBlob(_nativeObjectWriter, blobSize, blob);
        }

        [DllImport(NativeObjectWriterFileName)]
        static extern void EmitIntValue(IntPtr objWriter, ulong value, int size);
        public void EmitIntValue(ulong value, int size)
        {
            EmitIntValue(_nativeObjectWriter, value, size);
        }

        [DllImport(NativeObjectWriterFileName)]
        static extern void EmitSymbolDef(IntPtr objWriter, string symbolName);
        public void EmitSymbolDef(string symbolName)
        {
            EmitSymbolDef(_nativeObjectWriter, symbolName);
        }

        [DllImport(NativeObjectWriterFileName)]
        static extern void EmitSymbolRef(IntPtr objWriter, string symbolName, int size, bool isPCRelative, int delta = 0);
        public void EmitSymbolRef(string symbolName, int size, bool isPCRelative, int delta = 0)
        {
            EmitSymbolRef(_nativeObjectWriter, symbolName, size, isPCRelative, delta);
        }

        [DllImport(NativeObjectWriterFileName)]
        static extern void EmitFrameInfo(IntPtr objWriter, string frameName, int startOffset, int endOffset, int blobSize, byte[] blobData);
        public void EmitFrameInfo(string frameName, int startOffset, int endOffset, int blobSize, byte[] blobData)
        {
            EmitFrameInfo(_nativeObjectWriter, frameName, startOffset, endOffset, blobSize, blobData);
        }

        // This is one to multiple mapping -- we might have multiple symbols at the give offset.
        // We preserved the original order of ISymbolNode[].
        Dictionary<int, List<ISymbolNode>> _offsetToDefSymbol = new Dictionary<int, List<ISymbolNode>>();
        public void BuildSymbolDefinitionMap(ISymbolNode[] definedSymbols)
        {
            _offsetToDefSymbol.Clear();
            foreach (ISymbolNode n in definedSymbols)
            {
                if (!_offsetToDefSymbol.ContainsKey(n.Offset)) {
                    _offsetToDefSymbol[n.Offset] = new List<ISymbolNode>();
                }
                _offsetToDefSymbol[n.Offset].Add(n);
            }
        }

        public void EmitSymbolDefinition(int currentOffset)
        {
            List<ISymbolNode> nodes;
            if (_offsetToDefSymbol.TryGetValue(currentOffset, out nodes)) {
                foreach (var node in nodes)
                {
                    EmitSymbolDef(node.MangledName);
                }
            }
        }

        IntPtr _nativeObjectWriter = IntPtr.Zero;

        public ObjectWriter(string outputPath)
        {
            _nativeObjectWriter = InitObjWriter(outputPath);
            if (_nativeObjectWriter == IntPtr.Zero)
            {
                throw new IOException("Fail to initialize Native Object Writer");
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool bDisposing)
        {
            if (_nativeObjectWriter != null)
            {
                // Finalize object emission.
                FinishObjWriter(_nativeObjectWriter);
                _nativeObjectWriter = IntPtr.Zero;
            }

            if (bDisposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~ObjectWriter()
        {
            Dispose(false);
        }

        public static void EmitObject(string OutputPath, IEnumerable<DependencyNode> nodes, ISymbolNode mainMethodNode, NodeFactory factory)
        {
            using (ObjectWriter objectWriter = new ObjectWriter(OutputPath))
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
                        currentSection = node.Section;
                        objectWriter.SwitchSection(currentSection);
                    }

                    objectWriter.EmitAlignment(nodeContents.Alignment);

                    Relocation[] relocs = nodeContents.Relocs;
                    int nextRelocOffset = -1;
                    int nextRelocIndex = -1;
                    if (relocs != null && relocs.Length > 0)
                    {
                        nextRelocOffset = relocs[0].Offset;
                        nextRelocIndex = 0;
                    }

                    if (mainMethodNode == node)
                    {
                        objectWriter.EmitSymbolDef(MainEntryNodeName);
                    }

                    // Build symbol definition map.
                    objectWriter.BuildSymbolDefinitionMap(nodeContents.DefinedSymbols);

                    for (int i = 0; i < nodeContents.Data.Length; i++)
                    {
                        // Emit symbol definitions if necessary
                        objectWriter.EmitSymbolDefinition(i);

                        if (i == nextRelocOffset)
                        {
                            Relocation reloc = relocs[nextRelocIndex];

                            ISymbolNode target = reloc.Target;
                            string targetName = target.MangledName;
                            int size = 0;
                            bool isPCRelative = false;
                            switch (reloc.RelocType)
                            {
                                // REVIEW: I believe the JIT is emitting 0x3 instead of 0xA
                                // for x64, because emitter from x86 is ported for RyuJIT.
                                // I will consult with Bruce and if he agrees, I will delete
                                // this "case" duplicated by IMAGE_REL_BASED_DIR64.
                                case (RelocType)0x03: // IMAGE_REL_BASED_HIGHLOW
                                case RelocType.IMAGE_REL_BASED_DIR64:
                                    size = 8;
                                    break;
                                case RelocType.IMAGE_REL_BASED_REL32:
                                    size = 4;
                                    isPCRelative = true;
                                    break;
                                default:
                                    throw new NotImplementedException();
                            }
                            // Emit symbol reference
                            objectWriter.EmitSymbolRef(targetName, size, isPCRelative, reloc.Delta);

                            // Update nextRelocIndex/Offset
                            if (++nextRelocIndex < relocs.Length)
                            {
                                nextRelocOffset = relocs[nextRelocIndex].Offset;
                            }
                            i += size - 1;
                            continue;
                        }

                        objectWriter.EmitIntValue(nodeContents.Data[i], 1);
                    }

                    // It is possible to have a symbol just after all of the data.
                    objectWriter.EmitSymbolDefinition(nodeContents.Data.Length);

                    // Emit frame info for object code.
                    if (node is ObjectNodeWithFrameInfo) {
                        FrameInfo[] frameInfos = (node as ObjectNodeWithFrameInfo).GetFrameInfos();
                        if (frameInfos != null)
                        {
                            // The first definition is the main method name
                            string methodName = objectWriter._offsetToDefSymbol[0][0].MangledName;
                            foreach (var frameInfo in frameInfos) {
                                objectWriter.EmitFrameInfo(methodName,
                                    frameInfo.StartOffset,
                                    frameInfo.EndOffset,
                                    frameInfo.BlobData.Length,
                                    frameInfo.BlobData);
                            }
                            objectWriter.SwitchSection(currentSection);
                        }
                    }
                }

            }
        }
    }
}
