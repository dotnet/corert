// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;
using Internal.JitInterface;

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

        // Code offset to defined names
        private Dictionary<int, List<string>> _offsetToDefName = new Dictionary<int, List<string>>();

        // Code offset to Cfi blobs
        private Dictionary<int, List<byte[]>> _offsetToCfis = new Dictionary<int, List<byte[]>>();
        // Code offset to Lsda label index
        private Dictionary<int, string> _offsetToCfiLsdaBlobName = new Dictionary<int, string>();
        // Code offsets that starts a frame
        private HashSet<int> _offsetToCfiStart = new HashSet<int>();
        // Code offsets that ends a frame
        private HashSet<int> _offsetToCfiEnd = new HashSet<int>();
        // Used to assert whether frames are not overlapped.
        private bool _frameOpened;

        //  The size of CFI_CODE that RyuJit passes.
        private const int CfiCodeSize = 8;

        // The section name of the current node being processed.
        private string _currentSectionName;

        // The first defined symbol name of the current node being processed.
        private string _currentNodeName;

        // The set of custom section names that have been created so far
        private HashSet<string> _customSectionNames = new HashSet<string>();

        private const string NativeObjectWriterFileName = "objwriter";

        // Target platform ObjectWriter is instantiated for.
        private TargetDetails _targetPlatform;

        // Nodefactory for which ObjectWriter is instantiated for.
        private NodeFactory _nodeFactory;

        // Unix section containing LSDA data, like EH Info and GC Info
        public static readonly ObjectNodeSection LsdaSection = new ObjectNodeSection(".corert_eh_table", SectionType.ReadOnly);

#if DEBUG
        static HashSet<string> _previouslyWrittenNodeNames = new HashSet<string>();
#endif

        [DllImport(NativeObjectWriterFileName)]
        private static extern IntPtr InitObjWriter(string objectFilePath);

        [DllImport(NativeObjectWriterFileName)]
        private static extern void FinishObjWriter(IntPtr objWriter);

        [DllImport(NativeObjectWriterFileName)]
        private static extern void SwitchSection(IntPtr objWriter, string sectionName);
        public void SetSection(ObjectNodeSection section)
        {
            if (!section.IsStandardSection && !_customSectionNames.Contains(section.Name))
            {
                CreateCustomSection(section);
            }

            _currentSectionName = section.Name;
            SwitchSection(_nativeObjectWriter, section.Name);
        }

        public void EnsureCurrentSection()
        {
            SwitchSection(_nativeObjectWriter, _currentSectionName);
        }

        [Flags]
        public enum CustomSectionAttributes
        {
            ReadOnly = 0x0000,
            Writeable = 0x0001,
            Executable = 0x0002,
        };

        /// <summary>
        /// Builds a set of CustomSectionAttributes flags from an ObjectNodeSection.
        /// </summary>
        private CustomSectionAttributes GetCustomSectionAttributes(ObjectNodeSection section)
        {
            CustomSectionAttributes attributes = 0;

            switch (section.Type)
            {
                case SectionType.Executable:
                    attributes |= CustomSectionAttributes.Executable;
                    break;
                case SectionType.ReadOnly:
                    attributes |= CustomSectionAttributes.ReadOnly;
                    break;
                case SectionType.Writeable:
                    attributes |= CustomSectionAttributes.Writeable;
                    break;
            }

            return attributes;
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern bool CreateCustomSection(IntPtr objWriter, string sectionName, CustomSectionAttributes attributes, string comdatName);
        public void CreateCustomSection(ObjectNodeSection section)
        {
            CreateCustomSection(_nativeObjectWriter, section.Name, GetCustomSectionAttributes(section), section.ComdatName);
            _customSectionNames.Add(section.Name);
        }
        
        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitAlignment(IntPtr objWriter, int byteAlignment);
        public void EmitAlignment(int byteAlignment)
        {
            EmitAlignment(_nativeObjectWriter, byteAlignment);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitBlob(IntPtr objWriter, int blobSize, byte[] blob);
        public void EmitBlob(byte[] blob)
        {
            EmitBlob(_nativeObjectWriter, blob.Length, blob);
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
        private static extern int EmitSymbolRef(IntPtr objWriter, string symbolName, RelocType relocType, int delta);
        public int EmitSymbolRef(string symbolName, RelocType relocType, int delta = 0)
        {
            return EmitSymbolRef(_nativeObjectWriter, symbolName, relocType, delta);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitWinFrameInfo(IntPtr objWriter, string methodName, int startOffset, int endOffset, 
                                                    string blobSymbolName);
        public void EmitWinFrameInfo(int startOffset, int endOffset, int blobSize, string blobSymbolName)
        {
            EmitWinFrameInfo(_nativeObjectWriter, _currentNodeName, startOffset, endOffset, blobSymbolName);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitCFIStart(IntPtr objWriter, int nativeOffset);
        public void EmitCFIStart(int nativeOffset)
        {
            Debug.Assert(!_frameOpened);
            EmitCFIStart(_nativeObjectWriter, nativeOffset);
            _frameOpened = true;
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitCFIEnd(IntPtr objWriter, int nativeOffset);
        public void EmitCFIEnd(int nativeOffset)
        {
            Debug.Assert(_frameOpened);
            EmitCFIEnd(_nativeObjectWriter, nativeOffset);
            _frameOpened = false;
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitCFILsda(IntPtr objWriter, string blobSymbolName);
        public void EmitCFILsda(string blobSymbolName)
        {
            Debug.Assert(_frameOpened);
            EmitCFILsda(_nativeObjectWriter, blobSymbolName);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitCFICode(IntPtr objWriter, int nativeOffset, byte[] blob);
        public void EmitCFICode(int nativeOffset, byte[] blob)
        {
            Debug.Assert(_frameOpened);
            EmitCFICode(_nativeObjectWriter, nativeOffset, blob);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugFileInfo(IntPtr objWriter, int fileId, string fileName);
        public void EmitDebugFileInfo(int fileId, string fileName)
        {
            EmitDebugFileInfo(_nativeObjectWriter, fileId, fileName);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugLoc(IntPtr objWriter, int nativeOffset, int fileId, int linueNumber, int colNumber);
        public void EmitDebugLoc(int nativeOffset, int fileId, int linueNumber, int colNumber)
        {
            EmitDebugLoc(_nativeObjectWriter, nativeOffset, fileId, linueNumber, colNumber);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugVar(IntPtr objWriter, string name, UInt32 typeIndex, bool isParam, Int32 rangeCount, NativeVarInfo[] range);

        public void EmitDebugVar(DebugVarInfo debugVar)
        {
            int rangeCount = debugVar.Ranges.Count;
            EmitDebugVar(_nativeObjectWriter, debugVar.Name, debugVar.TypeIndex, debugVar.IsParam, rangeCount, debugVar.Ranges.ToArray());
        }

        public void EmitDebugVarInfo(ObjectNode node)
        {
            // No interest if it's not a debug node.
            var nodeWithDebugInfo = node as INodeWithDebugInfo;
            if (nodeWithDebugInfo != null)
            {
                DebugVarInfo[] vars = nodeWithDebugInfo.DebugVarInfos;
                if (vars != null)
                {
                    foreach (var v in vars)
                    {
                        EmitDebugVar(v);
                    }
                }
            }
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugFunctionInfo(IntPtr objWriter, string methodName, int methodSize);
        public void EmitDebugFunctionInfo(int methodSize)
        {
            EmitDebugFunctionInfo(_nativeObjectWriter, _currentNodeName, methodSize);
        }

        [DllImport(NativeObjectWriterFileName)]
        private static extern void EmitDebugModuleInfo(IntPtr objWriter);
        public void EmitDebugModuleInfo()
        {
            if (HasModuleDebugInfo())
            {
                EmitDebugModuleInfo(_nativeObjectWriter);
            }
        }

        public bool HasModuleDebugInfo()
        {
            return _debugFileToId.Count > 0;
        }

        public bool HasFunctionDebugInfo()
        {
            if (_offsetToDebugLoc.Count > 0)
            {
                Debug.Assert(HasModuleDebugInfo());
                return true;
            }

            return false;
        }

        public void BuildFileInfoMap(IEnumerable<DependencyNode> nodes)
        {
            int fileId = 1;
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
                                _debugFileToId.Add(fileName, fileId++);
                            }
                        }
                    }
                }
            }

            foreach (var entry in _debugFileToId)
            {
                this.EmitDebugFileInfo(entry.Value, entry.Key);
            }
        }

        public void BuildDebugLocInfoMap(ObjectNode node)
        {
            if (!HasModuleDebugInfo())
            {
                return;
            }

            _offsetToDebugLoc.Clear();
            INodeWithDebugInfo debugNode = node as INodeWithDebugInfo;
            if (debugNode != null)
            {
                DebugLocInfo[] locs = debugNode.DebugLocInfos;
                if (locs != null)
                {
                    foreach (var loc in locs)
                    {
                        _offsetToDebugLoc.Add(loc.NativeOffset, loc);
                    }
                }
            }
        }

        public void BuildCFIMap(NodeFactory factory, ObjectNode node)
        {
            _offsetToCfis.Clear();
            _offsetToCfiStart.Clear();
            _offsetToCfiEnd.Clear();
            _offsetToCfiLsdaBlobName.Clear();
            _frameOpened = false;

            INodeWithCodeInfo nodeWithCodeInfo = node as INodeWithCodeInfo;
            if (nodeWithCodeInfo == null)
            {
                return;
            }

            FrameInfo[] frameInfos = nodeWithCodeInfo.FrameInfos;
            if (frameInfos == null)
            {
                return;
            }

            ObjectNode.ObjectData ehInfo = nodeWithCodeInfo.EHInfo;

            int i = 0;
            foreach (var frameInfo in frameInfos)
            {
                int start = frameInfo.StartOffset;
                int end = frameInfo.EndOffset;
                int len = frameInfo.BlobData.Length;
                byte[] blob = frameInfo.BlobData;

                if (_targetPlatform.OperatingSystem == TargetOS.Windows)
                {
                    string blobSymbolName = "_unwind" + (i++).ToStringInvariant() + _currentNodeName;

                    ObjectNodeSection section = ObjectNodeSection.XDataSection;
                    if (node.ShouldShareNodeAcrossModules(factory) && factory.Target.OperatingSystem == TargetOS.Windows)
                    {
                        section = section.GetSharedSection(blobSymbolName);
                        CreateCustomSection(section);
                    }
                    SwitchSection(_nativeObjectWriter, section.Name);
                    
                    EmitAlignment(4);
                    EmitSymbolDef(blobSymbolName);

                    if (ehInfo != null)
                    {
                        blob[blob.Length - 1] |= 0x04; // Flag to indicate that EHClauses follows
                    }

                    EmitBlob(blob);

                    if (ehInfo != null)
                    {
                        Debug.Assert(ehInfo.Alignment == 1);
                        Debug.Assert(ehInfo.DefinedSymbols.Length == 0);
                        EmitBlobWithRelocs(ehInfo.Data, ehInfo.Relocs);
                        ehInfo = null;
                    }

                    // TODO: Currently we get linker errors if we emit frame info for shared types.
                    //       This needs follow-up investigation.
                    if (!node.ShouldShareNodeAcrossModules(factory))
                    {
                        // For window, just emit the frame blob (UNWIND_INFO) as a whole.
                        EmitWinFrameInfo(start, end, len, blobSymbolName);
                    }
                }
                else
                {
                    string blobSymbolName = "_lsda" + (i++).ToStringInvariant() + _currentNodeName;

                    SwitchSection(_nativeObjectWriter, LsdaSection.Name);

                    EmitAlignment(4);
                    EmitSymbolDef(blobSymbolName);

                    // emit relative offset from the main function
                    EmitIntValue((ulong)(start - frameInfos[0].StartOffset), 4);

                    // emit last byte from the blob - the function kind
                    byte funcKind = blob[len - 1];
                    if (ehInfo != null)
                    {
                        funcKind |= 0x04;
                    }
                    EmitIntValue(funcKind, 1);

                    --len;

                    if (ehInfo != null)
                    {
                        Debug.Assert(ehInfo.Alignment == 1);
                        Debug.Assert(ehInfo.DefinedSymbols.Length == 0);
                        EmitBlobWithRelocs(ehInfo.Data, ehInfo.Relocs);
                        ehInfo = null;
                    }

                    // For Unix, we build CFI blob map for each offset.
                    Debug.Assert(len % CfiCodeSize == 0);

                    // Record start/end of frames which shouldn't be overlapped.
                    _offsetToCfiStart.Add(start);
                    _offsetToCfiEnd.Add(end);
                    _offsetToCfiLsdaBlobName.Add(start, blobSymbolName);
                    for (int j = 0; j < len; j += CfiCodeSize)
                    {
                        // The first byte of CFI_CODE is offset from the range the frame covers.
                        // Compute code offset from the root method.
                        int codeOffset = blob[j] + start;
                        List<byte[]> cfis;
                        if (!_offsetToCfis.TryGetValue(codeOffset, out cfis))
                        {
                            cfis = new List<byte[]>();
                            _offsetToCfis.Add(codeOffset, cfis);
                        }
                        byte[] cfi = new byte[CfiCodeSize];
                        Array.Copy(blob, j, cfi, 0, CfiCodeSize);
                        cfis.Add(cfi);
                    }
                }

                EnsureCurrentSection();
            }
        }

        public void EmitCFICodes(int offset)
        {
            // Emit end the old frame before start a frame.
            if (_offsetToCfiEnd.Contains(offset))
            {
                EmitCFIEnd(offset);
            }

            if (_offsetToCfiStart.Contains(offset))
            {
                EmitCFIStart(offset);

                string blobSymbolName = ""; 
                if (_offsetToCfiLsdaBlobName.TryGetValue(offset, out blobSymbolName))
                {
                    EmitCFILsda(blobSymbolName);
                }
                else
                {
                    // Internal compiler error
                    Debug.Assert(false);
                }
            }

            // Emit individual cfi blob for the given offset
            List<byte[]> cfis;
            if (_offsetToCfis.TryGetValue(offset, out cfis))
            {
                foreach(byte[] cfi in cfis)
                {
                    EmitCFICode(offset, cfi);
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

        public void BuildSymbolDefinitionMap(ObjectNode node, ISymbolNode[] definedSymbols)
        {
            _offsetToDefName.Clear();
            foreach (ISymbolNode n in definedSymbols)
            {
                if (!_offsetToDefName.ContainsKey(n.Offset))
                {
                    _offsetToDefName[n.Offset] = new List<string>();
                }

                string symbolToEmit = GetSymbolToEmitForTargetPlatform(n.MangledName);
                _offsetToDefName[n.Offset].Add(symbolToEmit);

                string alternateName = _nodeFactory.GetSymbolAlternateName(n);
                if (alternateName != null)
                {
                    symbolToEmit = GetSymbolToEmitForTargetPlatform(alternateName);
                    _offsetToDefName[n.Offset].Add(symbolToEmit);
                }
            }

            var symbolNode = node as ISymbolNode;
            if (symbolNode != null)
            {
                _currentNodeName = GetSymbolToEmitForTargetPlatform(symbolNode.MangledName);
                Debug.Assert(_offsetToDefName[symbolNode.Offset].Contains(_currentNodeName));
            }
            else
            {
                _currentNodeName = null;
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

        // Returns size of the emitted symbol reference
        public int EmitSymbolReference(ISymbolNode target, int delta, RelocType relocType)
        {
            string targetName = GetSymbolToEmitForTargetPlatform(target.MangledName);

            return EmitSymbolRef(targetName, relocType, delta);
        }

        public void EmitBlobWithRelocs(byte[] blob, Relocation[] relocs)
        {
            int nextRelocOffset = -1;
            int nextRelocIndex = -1;
            if (relocs.Length > 0)
            {
                nextRelocOffset = relocs[0].Offset;
                nextRelocIndex = 0;
            }

            int i = 0;
            while (i < blob.Length)
            {
                if (i == nextRelocOffset)
                {
                    Relocation reloc = relocs[nextRelocIndex];

                    int size = EmitSymbolReference(reloc.Target, reloc.Delta, reloc.RelocType);

                    // Update nextRelocIndex/Offset
                    if (++nextRelocIndex < relocs.Length)
                    {
                        nextRelocOffset = relocs[nextRelocIndex].Offset;
                    }
                    i += size;
                }
                else
                {
                    EmitIntValue(blob[i], 1);
                    i++;
                }
            }
        }

        public void EmitSymbolDefinition(int currentOffset)
        {
            List<string> nodes;
            if (_offsetToDefName.TryGetValue(currentOffset, out nodes))
            {
                foreach (var name in nodes)
                {
                    EmitSymbolDef(name);
                }
            }
        }

        private IntPtr _nativeObjectWriter = IntPtr.Zero;

        public ObjectWriter(string objectFilePath, NodeFactory factory)
        {
            _nativeObjectWriter = InitObjWriter(objectFilePath);
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

        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, NodeFactory factory)
        {
            using (ObjectWriter objectWriter = new ObjectWriter(objectFilePath, factory))
            {
                if (factory.Target.OperatingSystem == TargetOS.Windows)
                {
                    objectWriter.CreateCustomSection(MethodCodeNode.WindowsContentSection);

                    // Emit sentinels for managed code section.
                    ObjectNodeSection codeStartSection = factory.CompilationModuleGroup.IsSingleFileCompilation ?
                                                            MethodCodeNode.StartSection :
                                                            MethodCodeNode.StartSection.GetSharedSection("__managedcode_a");
                    objectWriter.SetSection(codeStartSection);
                    objectWriter.EmitSymbolDef("__managedcode_a");
                    objectWriter.EmitIntValue(0, 1);
                    ObjectNodeSection codeEndSection = factory.CompilationModuleGroup.IsSingleFileCompilation ?
                                                            MethodCodeNode.EndSection :
                                                            MethodCodeNode.EndSection.GetSharedSection("__managedcode_z");
                    objectWriter.SetSection(codeEndSection);
                    objectWriter.EmitSymbolDef("__managedcode_z");
                    objectWriter.EmitIntValue(0, 1);
                }
                else
                {
                    objectWriter.CreateCustomSection(MethodCodeNode.UnixContentSection);
                    objectWriter.CreateCustomSection(LsdaSection);
                }

                // Build file info map.
                objectWriter.BuildFileInfoMap(nodes);

                foreach (DependencyNode depNode in nodes)
                {
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(factory))
                        continue;

#if DEBUG
                    Debug.Assert(_previouslyWrittenNodeNames.Add(node.GetName()), "Duplicate node name emitted to file", "Node {0} has already been written to the output object file {1}", node.GetName(), objectFilePath);
#endif
                    ObjectNode.ObjectData nodeContents = node.GetData(factory);

                    ObjectNodeSection section = node.Section;
                    if (node.ShouldShareNodeAcrossModules(factory) && factory.Target.OperatingSystem == TargetOS.Windows)
                    {
                        Debug.Assert(node is ISymbolNode);
                        section = section.GetSharedSection(((ISymbolNode)node).MangledName);
                    }

                    // Ensure section and alignment for the node.
                    objectWriter.SetSection(section);
                    objectWriter.EmitAlignment(nodeContents.Alignment);

                    // Build symbol definition map.
                    objectWriter.BuildSymbolDefinitionMap(node, nodeContents.DefinedSymbols);

                    // Build CFI map (Unix) or publish unwind blob (Windows).
                    objectWriter.BuildCFIMap(factory, node);

                    // Build debug location map
                    objectWriter.BuildDebugLocInfoMap(node);

                    Relocation[] relocs = nodeContents.Relocs;
                    int nextRelocOffset = -1;
                    int nextRelocIndex = -1;
                    if (relocs.Length > 0)
                    {
                        nextRelocOffset = relocs[0].Offset;
                        nextRelocIndex = 0;
                    }

                    int i = 0;
                    while (i < nodeContents.Data.Length)
                    {
                        // Emit symbol definitions if necessary
                        objectWriter.EmitSymbolDefinition(i);

                        // Emit CFI codes for the given offset.
                        objectWriter.EmitCFICodes(i);

                        // Emit debug loc info if needed.
                        objectWriter.EmitDebugLocInfo(i);

                        if (i == nextRelocOffset)
                        {
                            Relocation reloc = relocs[nextRelocIndex];

                            int size = objectWriter.EmitSymbolReference(reloc.Target, reloc.Delta, reloc.RelocType);

                            // Update nextRelocIndex/Offset
                            if (++nextRelocIndex < relocs.Length)
                            {
                                nextRelocOffset = relocs[nextRelocIndex].Offset;
                            }
                            i += size;
                        }
                        else
                        {
                            objectWriter.EmitIntValue(nodeContents.Data[i], 1);
                            i++;
                        }
                    }

                    // It is possible to have a symbol just after all of the data.
                    objectWriter.EmitSymbolDefinition(nodeContents.Data.Length);

                    // Emit the last CFI to close the frame.
                    objectWriter.EmitCFICodes(nodeContents.Data.Length);

                    if (objectWriter.HasFunctionDebugInfo())
                    {
                        // Build debug local var info
                        objectWriter.EmitDebugVarInfo(node);

                        objectWriter.EmitDebugFunctionInfo(nodeContents.Data.Length);
                    }
                }

                objectWriter.EmitDebugModuleInfo();
            }
        }
    }
}
