// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ILCompiler.DependencyAnalysisFramework;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer using https://github.com/dotnet/llilc
    /// </summary>
    internal class ObjectWriter : IDisposable
    {
        // This is used to look up file id for the given file name.
        // This is a global table across nodes.
        private Dictionary<string, int> _debugFileToId = new Dictionary<string, int>();

        // This is used to look up DebugLocInfo for the given native offset.
        // This is for individual node and should be flushed once node is emitted.
        private Dictionary<int, DebugLocInfo> _offsetToDebugLoc = new Dictionary<int, DebugLocInfo>();

        // This is one to multiple mapping -- we might have multiple symbols at the give offset.
        // We preserved the original order of ISymbolNode[].
        private Dictionary<int, List<ISymbolNode>> _offsetToDefSymbol = new Dictionary<int, List<ISymbolNode>>();

        private const string NativeObjectWriterFileName = "objwriter";

        // Target platform ObjectWriter is instantiated for.
        private TargetDetails _targetPlatform;

        // Nodefactory for which ObjectWriter is instantiated for.
        private NodeFactory _nodeFactory;

        [DllImport(NativeObjectWriterFileName)]
        private static extern IntPtr InitObjWriter(string objectFilePath);

        [DllImport(NativeObjectWriterFileName)]
        private static extern void FinishObjWriter(IntPtr objWriter);

        [DllImport(NativeObjectWriterFileName)]
        private static extern void SwitchSection(IntPtr objWriter, string sectionName);
        public void SwitchSection(string sectionName)
        {
            SwitchSection(_nativeObjectWriter, sectionName);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitAlignment(IntPtr objWriter, int byteAlignment);
        public void EmitAlignment(int byteAlignment)
        {
            EmitAlignment(_nativeObjectWriter, byteAlignment);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitBlob(IntPtr objWriter, int blobSize, byte[] blob);
        public void EmitBlob(int blobSize, byte[] blob)
        {
            EmitBlob(_nativeObjectWriter, blobSize, blob);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitIntValue(IntPtr objWriter, ulong value, int size);
        public void EmitIntValue(ulong value, int size)
        {
            EmitIntValue(_nativeObjectWriter, value, size);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitSymbolDef(IntPtr objWriter, string symbolName);
        public void EmitSymbolDef(string symbolName)
        {
            EmitSymbolDef(_nativeObjectWriter, symbolName);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitSymbolRef(IntPtr objWriter, string symbolName, int size, bool isPCRelative, int delta = 0);
        public void EmitSymbolRef(string symbolName, int size, bool isPCRelative, int delta = 0)
        {
            EmitSymbolRef(_nativeObjectWriter, symbolName, size, isPCRelative, delta);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitFrameInfo(IntPtr objWriter, string methodName, int startOffset, int endOffset, int blobSize, byte[] blobData);
        public void EmitFrameInfo(string methodName, int startOffset, int endOffset, int blobSize, byte[] blobData)
        {
            EmitFrameInfo(_nativeObjectWriter, methodName, startOffset, endOffset, blobSize, blobData);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugFileInfo(IntPtr objWriter, int fileInfoSize, string[] fileInfos);
        public void EmitDebugFileInfo(int fileInfoSize, string[] fileInfos)
        {
            EmitDebugFileInfo(_nativeObjectWriter, fileInfoSize, fileInfos);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugLoc(IntPtr objWriter, int nativeOffset, int fileId, int linueNumber, int colNumber);
        public void EmitDebugLoc(int nativeOffset, int fileId, int linueNumber, int colNumber)
        {
            EmitDebugLoc(_nativeObjectWriter, nativeOffset, fileId, linueNumber, colNumber);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void FlushDebugLocs(IntPtr objWriter, string methodName, int methodSize);
        public void FlushDebugLocs(string methodName, int methodSize)
        {
            // No interest if there is no debug location emission/map before.
            if (_offsetToDebugLoc.Count == 0)
            {
                return;
            }
            FlushDebugLocs(_nativeObjectWriter, methodName, methodSize);

            // Ensure clean up the map for the next node.
            _offsetToDebugLoc.Clear();
        }

        public string[] BuildFileInfoMap(IEnumerable<DependencyNode> nodes)
        {
            ArrayBuilder<string> debugFileInfos = new ArrayBuilder<string>();
            foreach (DependencyNode node in nodes)
            {
                if (node is INodeWithDebugInfo)
                {
                    DebugLocInfo[] debugLocInfos = ((INodeWithDebugInfo)node).DebugLocInfos;
                    if (debugLocInfos != null)
                    {
                        foreach (DebugLocInfo debugLocInfo in debugLocInfos)
                        {
                            string fileName = debugLocInfo.FileName;
                            if (!_debugFileToId.ContainsKey(fileName))
                            {
                                _debugFileToId.Add(fileName, debugFileInfos.Count);
                                debugFileInfos.Add(fileName);
                            }
                        }
                    }
                }
            }

            return debugFileInfos.Count > 0 ? debugFileInfos.ToArray() : null;
        }

        public void BuildDebugLocInfoMap(ObjectNode node)
        {
            // No interest if file map is no built before.
            if (_debugFileToId.Count == 0)
            {
                return;
            }

            // No interest if it's not a debug node.
            if (!(node is INodeWithDebugInfo))
            {
                return;
            }

            DebugLocInfo[] locs = (node as INodeWithDebugInfo).DebugLocInfos;
            if (locs != null)
            {
                foreach (var loc in locs)
                {
                    _offsetToDebugLoc.Add(loc.NativeOffset, loc);
                }
            }
        }

        public void EmitDebugLocInfo(int offset)
        {
            DebugLocInfo loc;
            if (_offsetToDebugLoc.TryGetValue(offset, out loc))
            {
                Debug.Assert(_debugFileToId.Count > 0);
                EmitDebugLoc(offset,
                    _debugFileToId[loc.FileName],
                    loc.LineNumber,
                    loc.ColNumber);
            }
        }

        public void BuildSymbolDefinitionMap(ISymbolNode[] definedSymbols)
        {
            _offsetToDefSymbol.Clear();
            foreach (ISymbolNode n in definedSymbols)
            {
                if (!_offsetToDefSymbol.ContainsKey(n.Offset))
                {
                    _offsetToDefSymbol[n.Offset] = new List<ISymbolNode>();
                }
                _offsetToDefSymbol[n.Offset].Add(n);
            }
        }

        private string GetSymbolToEmitForTargetPlatform(string symbol)
        {
            string symbolToEmit = symbol;

            if (_targetPlatform.OperatingSystem == TargetOS.OSX)
            {
                // On OSX, we need to prefix an extra underscore to account for correct linkage of 
                // extern "C" functions.
                symbolToEmit = "_"+symbol;
            }

            return symbolToEmit;
        }

        public void EmitSymbolDefinition(int currentOffset)
        {
            List<ISymbolNode> nodes;
            if (_offsetToDefSymbol.TryGetValue(currentOffset, out nodes))
            {
                foreach (var node in nodes)
                {
                    string symbolToEmit = GetSymbolToEmitForTargetPlatform(node.MangledName);
                    EmitSymbolDef(symbolToEmit);

                    string alternateName = _nodeFactory.GetSymbolAlternateName(node);
                    if (alternateName != null)
                    {
                        symbolToEmit = GetSymbolToEmitForTargetPlatform(alternateName);
                        EmitSymbolDef(symbolToEmit);
                    }
                }
            }
        }

        private IntPtr _nativeObjectWriter = IntPtr.Zero;

        public ObjectWriter(string outputPath, NodeFactory factory)
        {
            _nativeObjectWriter = InitObjWriter(outputPath);
            if (_nativeObjectWriter == IntPtr.Zero)
            {
                throw new IOException("Fail to initialize Native Object Writer");
            }

            _nodeFactory = factory;
            _targetPlatform = _nodeFactory.Target;
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

            _nodeFactory = null;

            if (bDisposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~ObjectWriter()
        {
            Dispose(false);
        }

        public static void EmitObject(string OutputPath, IEnumerable<DependencyNode> nodes, NodeFactory factory)
        {
            using (ObjectWriter objectWriter = new ObjectWriter(OutputPath, factory))
            {
                string currentSection = "";

                // Build file info map.
                string[] debugFileInfos = objectWriter.BuildFileInfoMap(nodes);
                if (debugFileInfos != null)
                {
                    objectWriter.EmitDebugFileInfo(debugFileInfos.Length, debugFileInfos);
                }

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

                    // Build symbol definition map.
                    objectWriter.BuildSymbolDefinitionMap(nodeContents.DefinedSymbols);

                    // Build debug location map
                    objectWriter.BuildDebugLocInfoMap(node);

                    for (int i = 0; i < nodeContents.Data.Length; i++)
                    {
                        // Emit symbol definitions if necessary
                        objectWriter.EmitSymbolDefinition(i);

                        // Emit debug loc info if needed.
                        objectWriter.EmitDebugLocInfo(i);

                        if (i == nextRelocOffset)
                        {
                            Relocation reloc = relocs[nextRelocIndex];

                            ISymbolNode target = reloc.Target;
                            string targetName = objectWriter.GetSymbolToEmitForTargetPlatform(target.MangledName);
                            int size = 0;
                            bool isPCRelative = false;
                            switch (reloc.RelocType)
                            {
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

                    // The first definition is the main node name
                    string nodeName = objectWriter._offsetToDefSymbol[0][0].MangledName;
                    // Emit frame info for object code.
                    if (node is INodeWithFrameInfo)
                    {
                        FrameInfo[] frameInfos = ((INodeWithFrameInfo)node).FrameInfos;
                        if (frameInfos != null)
                        {
                            foreach (var frameInfo in frameInfos)
                            {
                                objectWriter.EmitFrameInfo(nodeName,
                                    frameInfo.StartOffset,
                                    frameInfo.EndOffset,
                                    frameInfo.BlobData.Length,
                                    frameInfo.BlobData);
                            }
                        }
                    }

                    objectWriter.FlushDebugLocs(nodeName, nodeContents.Data.Length);
                    objectWriter.SwitchSection(currentSection);
                }
            }
        }
    }
}
