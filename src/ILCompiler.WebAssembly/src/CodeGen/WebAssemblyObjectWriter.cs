// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using ILCompiler;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.WebAssembly;
using Internal.Text;
using Internal.TypeSystem;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

using LLVMSharp.Interop;
using ILCompiler.DependencyAnalysis;
using Internal.IL;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer using https://github.com/dotnet/llilc
    /// </summary>
    internal class WebAssemblyObjectWriter : IDisposable
    {
        private string GetBaseSymbolName(ISymbolNode symbol, NameMangler nameMangler, bool objectWriterUse = false)
        {
            if (symbol is WebAssemblyMethodCodeNode || symbol is WebAssemblyBlockRefNode)
            {
                return symbol.GetMangledName(nameMangler);
            }

            if (symbol is ObjectNode)
            {
                ISymbolDefinitionNode symbolDefNode = (ISymbolDefinitionNode)symbol;
                var symbolName = _nodeFactory.GetSymbolAlternateName(symbolDefNode) ?? symbol.GetMangledName(nameMangler);
                if (symbolDefNode.Offset == 0)
                {
                    return symbolName;
                }
                else
                {
                    return symbolName + "___REALBASE";
                }
            }
            else if (symbol is ObjectAndOffsetSymbolNode)
            {
                ObjectAndOffsetSymbolNode objAndOffset = (ObjectAndOffsetSymbolNode)symbol;
                if (objAndOffset.Target is IHasStartSymbol)
                {
                    ISymbolNode startSymbol = ((IHasStartSymbol)objAndOffset.Target).StartSymbol;
                    if (startSymbol == symbol)
                    {
                        Debug.Assert(startSymbol.Offset == 0);
                        return symbol.GetMangledName(nameMangler);
                    }
                    return GetBaseSymbolName(startSymbol, nameMangler, objectWriterUse);
                }
                return GetBaseSymbolName((ISymbolNode)objAndOffset.Target, nameMangler, objectWriterUse);
            }
            else if (symbol is EmbeddedObjectNode)
            {
                EmbeddedObjectNode embeddedNode = (EmbeddedObjectNode)symbol;
                return GetBaseSymbolName(embeddedNode.ContainingNode.StartSymbol, nameMangler, objectWriterUse);
            }
            else
            {
                return null;
            }
        }

        public static Dictionary<string, LLVMValueRef> s_symbolValues = new Dictionary<string, LLVMValueRef>();
        private static Dictionary<FieldDesc, LLVMValueRef> s_staticFieldMapping = new Dictionary<FieldDesc, LLVMValueRef>();

        public static LLVMValueRef GetSymbolValuePointer(LLVMModuleRef module, ISymbolNode symbol, NameMangler nameMangler, bool objectWriterUse = false)
        {
            if (symbol is WebAssemblyMethodCodeNode)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            string symbolAddressGlobalName = symbol.GetMangledName(nameMangler) + "___SYMBOL";
            LLVMValueRef symbolAddress;
            if (s_symbolValues.TryGetValue(symbolAddressGlobalName, out symbolAddress))
            {
                return symbolAddress;
            }
            var intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
            var myGlobal = module.AddGlobalInAddressSpace(intPtrType, symbolAddressGlobalName, 0);
            myGlobal.IsGlobalConstant = true;
            myGlobal.Linkage = LLVMLinkage.LLVMInternalLinkage;
            s_symbolValues.Add(symbolAddressGlobalName, myGlobal);
            return myGlobal;
        }

        private static int GetNumericOffsetFromBaseSymbolValue(ISymbolNode symbol)
        {
            if (symbol is WebAssemblyMethodCodeNode || symbol is WebAssemblyBlockRefNode)
            {
                return 0;
            }

            if (symbol is ObjectNode)
            {
                ISymbolDefinitionNode symbolDefNode = (ISymbolDefinitionNode)symbol;
                return symbolDefNode.Offset;
            }
            else if (symbol is ObjectAndOffsetSymbolNode)
            {
                ObjectAndOffsetSymbolNode objAndOffset = (ObjectAndOffsetSymbolNode)symbol;
                ISymbolDefinitionNode symbolDefNode = (ISymbolDefinitionNode)symbol;
                if (objAndOffset.Target is IHasStartSymbol)
                {
                    ISymbolNode startSymbol = ((IHasStartSymbol)objAndOffset.Target).StartSymbol;
                    
                    if (startSymbol == symbol)
                    {
                        Debug.Assert(symbolDefNode.Offset == 0);
                        return symbolDefNode.Offset;
                    }
                    return symbolDefNode.Offset;
                }
                int baseOffset = GetNumericOffsetFromBaseSymbolValue((ISymbolNode)objAndOffset.Target);
                return baseOffset + symbolDefNode.Offset;
            }
            else if (symbol is EmbeddedObjectNode)
            {
                EmbeddedObjectNode embeddedNode = (EmbeddedObjectNode)symbol;
                int baseOffset = GetNumericOffsetFromBaseSymbolValue(embeddedNode.ContainingNode.StartSymbol);
                return baseOffset + ((ISymbolDefinitionNode)embeddedNode).Offset;
            }
            else
            {
                ThrowHelper.ThrowInvalidProgramException();
                return 0;
            }
        }

        // this is the llvm instance.
        public LLVMModuleRef Module { get; }

        public LLVMDIBuilderRef DIBuilder { get; }

        // This is used to build mangled names
        private Utf8StringBuilder _sb = new Utf8StringBuilder();

        // Track offsets in node data that prevent writing all bytes in one single blob. This includes
        // relocs, symbol definitions, debug data that must be streamed out using the existing LLVM API
        private SortedSet<int> _byteInterruptionOffsets = new SortedSet<int>();

        // Code offset to defined names
        private Dictionary<int, List<ISymbolDefinitionNode>> _offsetToDefName = new Dictionary<int, List<ISymbolDefinitionNode>>();

        // The section for the current node being processed.
        private ObjectNodeSection _currentSection;

        // The first defined symbol name of the current node being processed.
        private Utf8String _currentNodeZeroTerminatedName;

        // Nodefactory for which ObjectWriter is instantiated for.
        private NodeFactory _nodeFactory;

#if DEBUG
        static Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new Dictionary<string, ISymbolNode>();
#endif

        public void SetSection(ObjectNodeSection section)
        {
            _currentSection = section;
        }

        public void FinishObjWriter(LLVMContextRef context)
        {
            // Since emission to llvm is delayed until after all nodes are emitted... emit now.
            foreach (var nodeData in _dataToFill)
            {
                nodeData.Fill(Module, _nodeFactory);
            }

            EmitNativeMain(context);

            EmitDebugMetadata(context);

#if DEBUG
            Module.PrintToFile(Path.ChangeExtension(_objectFilePath, ".txt"));
#endif //DEBUG
            Module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);

            Module.WriteBitcodeToFile(_objectFilePath);

            //throw new NotImplementedException(); // This function isn't complete
        }

        private void EmitDebugMetadata(LLVMContextRef context)
        {
            var dwarfVersion = LLVMValueRef.CreateMDNode(new[]
            {
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false),
                context.GetMDString("Dwarf Version", 13),
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 4, false)
            });
            var dwarfSchemaVersion = LLVMValueRef.CreateMDNode(new[]
            {
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false),
                context.GetMDString("Debug Info Version", 18),
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 3, false)
            });
            Module.AddNamedMetadataOperand("llvm.module.flags", dwarfVersion);
            Module.AddNamedMetadataOperand("llvm.module.flags", dwarfSchemaVersion);
            DIBuilder.DIBuilderFinalize();
        }

        private void EmitReadyToRunHeaderCallback(LLVMContextRef context)
        {
            LLVMTypeRef intPtr = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
            LLVMTypeRef intPtrPtr = LLVMTypeRef.CreatePointer(intPtr, 0);
            var callback = Module.AddFunction("RtRHeaderWrapper", LLVMTypeRef.CreateFunction(intPtrPtr, new LLVMTypeRef[0], false));
            var builder = context.CreateBuilder();
            var block = callback.AppendBasicBlock("Block");
            builder.PositionAtEnd(block);

            LLVMValueRef rtrHeaderPtr = GetSymbolValuePointer(Module, _nodeFactory.ReadyToRunHeader, _nodeFactory.NameMangler, false);
            LLVMValueRef castRtrHeaderPtr = builder.BuildPointerCast(rtrHeaderPtr, intPtrPtr, "castRtrHeaderPtr");
            builder.BuildRet(castRtrHeaderPtr);
        }

        private void EmitNativeMain(LLVMContextRef context)
        {
            LLVMValueRef shadowStackTop = Module.GetNamedGlobal("t_pShadowStackTop");

            LLVMBuilderRef builder = context.CreateBuilder();
            var mainSignature = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new LLVMTypeRef[] { LLVMTypeRef.Int32, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            var mainFunc = Module.AddFunction("__managed__Main", mainSignature);
            var mainEntryBlock = mainFunc.AppendBasicBlock("entry");
            builder.PositionAtEnd(mainEntryBlock);
            LLVMValueRef managedMain = Module.GetNamedFunction("StartupCodeMain");
            if (managedMain.Handle == IntPtr.Zero)
            {
                throw new Exception("Main not found");
            }

            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef reversePinvokeFrame = builder.BuildAlloca(reversePInvokeFrameType, "ReversePInvokeFrame");
            LLVMValueRef RhpReversePInvoke2 = Module.GetNamedFunction("RhpReversePInvoke2");

            if (RhpReversePInvoke2.Handle == IntPtr.Zero)
            {
                RhpReversePInvoke2 = Module.AddFunction("RhpReversePInvoke2", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false));
            }

            builder.BuildCall(RhpReversePInvoke2, new LLVMValueRef[] { reversePinvokeFrame }, "");

            var shadowStack = builder.BuildMalloc(LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, 1000000), String.Empty);
            var castShadowStack = builder.BuildPointerCast(shadowStack, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), String.Empty);
            builder.BuildStore(castShadowStack, shadowStackTop);

            var shadowStackBottom = Module.AddGlobal(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "t_pShadowStackBottom");
            shadowStackBottom.Linkage = LLVMLinkage.LLVMExternalLinkage;
            shadowStackBottom.Initializer = LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            shadowStackBottom.ThreadLocalMode = LLVMThreadLocalMode.LLVMLocalDynamicTLSModel;
            builder.BuildStore(castShadowStack, shadowStackBottom);

            // Pass on main arguments
            LLVMValueRef argc = mainFunc.GetParam(0);
            LLVMValueRef argv = mainFunc.GetParam(1);

            LLVMValueRef mainReturn = builder.BuildCall(managedMain, new LLVMValueRef[]
            {
                castShadowStack,
                argc,
                argv,
            },
            "returnValue");

            LLVMValueRef RhpReversePInvokeReturn2 = Module.GetNamedFunction("RhpReversePInvokeReturn2");
            LLVMTypeRef reversePInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false);
            if (RhpReversePInvoke2.Handle == IntPtr.Zero)
            {
                RhpReversePInvokeReturn2 = Module.AddFunction("RhpReversePInvokeReturn2", reversePInvokeFunctionType);
            }
            builder.BuildCall(RhpReversePInvokeReturn2, new LLVMValueRef[] { reversePinvokeFrame }, "");

            builder.BuildRet(mainReturn);
            mainFunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
        }

        public void SetCodeSectionAttribute(ObjectNodeSection section)
        {
            //throw new NotImplementedException(); // This function isn't complete
        }

        public void EnsureCurrentSection()
        {
        }

        ArrayBuilder<byte> _currentObjectData = new ArrayBuilder<byte>();
        struct SymbolRefData
        {
            public SymbolRefData(bool isFunction, string symbolName, uint offset)
            {
                IsFunction = isFunction;
                SymbolName = symbolName;
                Offset = offset;
            }

            readonly bool IsFunction;
            readonly string SymbolName;
            readonly uint Offset;

            public LLVMValueRef ToLLVMValueRef(LLVMModuleRef module)
            {
                LLVMValueRef valRef = IsFunction ? module.GetNamedFunction(SymbolName) : module.GetNamedGlobal(SymbolName);

                if (Offset != 0 && valRef.Handle != IntPtr.Zero)
                {
                    var CreatePointer = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                    var bitCast = LLVMValueRef.CreateConstBitCast(valRef, CreatePointer);
                    LLVMValueRef[] index = new LLVMValueRef[] {LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, Offset, false)};
                    valRef = LLVMValueRef.CreateConstGEP(bitCast, index);
                }

                return valRef;
            }
        }

        Dictionary<int, SymbolRefData> _currentObjectSymbolRefs = new Dictionary<int, SymbolRefData>();
        ObjectNode _currentObjectNode;

        List<ObjectNodeDataEmission> _dataToFill = new List<ObjectNodeDataEmission>();

        List<KeyValuePair<string, int>> _symbolDefs = new List<KeyValuePair<string, int>>();

        struct ObjectNodeDataEmission
        {
            public ObjectNodeDataEmission(LLVMValueRef node, byte[] data, Dictionary<int, SymbolRefData> objectSymbolRefs)
            {
                Node = node;
                Data = data;
                ObjectSymbolRefs = objectSymbolRefs;
            }
            LLVMValueRef Node;
            readonly byte[] Data;
            readonly Dictionary<int, SymbolRefData> ObjectSymbolRefs;

            public void Fill(LLVMModuleRef module, NodeFactory nodeFactory)
            {
                List<LLVMValueRef> entries = new List<LLVMValueRef>();
                int pointerSize = nodeFactory.Target.PointerSize;

                int countOfPointerSizedElements = Data.Length / pointerSize;

                byte[] currentObjectData = Data;
                var intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);

                for (int i = 0; i < countOfPointerSizedElements; i++)
                {
                    int curOffset = (i * pointerSize);
                    SymbolRefData symbolRef;
                    if (ObjectSymbolRefs.TryGetValue(curOffset, out symbolRef))
                    {
                        LLVMValueRef pointedAtValue = symbolRef.ToLLVMValueRef(module);
                        var ptrValue = LLVMValueRef.CreateConstBitCast(pointedAtValue, intPtrType);
                        entries.Add(ptrValue);
                    }
                    else
                    {
                        int value = BitConverter.ToInt32(currentObjectData, curOffset);
                        entries.Add(LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)value, false), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0)));
                    }
                }

                var funcptrarray = LLVMValueRef.CreateConstArray(intPtrType, entries.ToArray());
                Node.Initializer = funcptrarray;
            }
        }

        public void StartObjectNode(ObjectNode node)
        {
            Debug.Assert(_currentObjectNode == null);
            _currentObjectNode = node;
            Debug.Assert(_currentObjectData.Count == 0);
        }

        public void DoneObjectNode()
        {
            EmitAlignment(_nodeFactory.Target.PointerSize);
            Debug.Assert(_nodeFactory.Target.PointerSize == 4);
            int countOfPointerSizedElements = _currentObjectData.Count / _nodeFactory.Target.PointerSize;

            ISymbolNode symNode = _currentObjectNode as ISymbolNode;
            if (symNode == null)
                symNode = ((IHasStartSymbol)_currentObjectNode).StartSymbol;
            string realName = GetBaseSymbolName(symNode, _nodeFactory.NameMangler, true);

            var intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
            var arrayglobal = Module.AddGlobalInAddressSpace(LLVMTypeRef.CreateArray(intPtrType, (uint)countOfPointerSizedElements), realName, 0);
            arrayglobal.Linkage = LLVMLinkage.LLVMExternalLinkage;


            _dataToFill.Add(new ObjectNodeDataEmission(arrayglobal, _currentObjectData.ToArray(), _currentObjectSymbolRefs));

            foreach (var symbolIdInfo in _symbolDefs)
            {
                EmitSymbolDef(arrayglobal, symbolIdInfo.Key, symbolIdInfo.Value);
            }

            _currentObjectNode = null;
            _currentObjectSymbolRefs = new Dictionary<int, SymbolRefData>();
            _currentObjectData = new ArrayBuilder<byte>();
            _symbolDefs.Clear();
        }

        public void EmitAlignment(int byteAlignment)
        {
            while ((_currentObjectData.Count % byteAlignment) != 0)
                _currentObjectData.Add(0);
        }

        public void EmitBlob(byte[] blob)
        {
            _currentObjectData.Append(blob);
        }
        
        public void EmitIntValue(ulong value, int size)
        {
            switch (size)
            {
                case 1:
                    _currentObjectData.Append(BitConverter.GetBytes((byte)value));
                    break;
                case 2:
                    _currentObjectData.Append(BitConverter.GetBytes((ushort)value));
                    break;
                case 4:
                    _currentObjectData.Append(BitConverter.GetBytes((uint)value));
                    break;
                case 8:
                    _currentObjectData.Append(BitConverter.GetBytes(value));
                    break;
                default:
                    ThrowHelper.ThrowInvalidProgramException();
                    break;
            }
        }

        public void EmitBytes(IntPtr pArray, int length)
        {
            unsafe
            {
                byte* pBytes = (byte*)pArray;
                for (int i = 0; i < length; i++)
                    _currentObjectData.Add(pBytes[i]);
            }
        }
        
        public void EmitSymbolDef(LLVMValueRef realSymbol, string symbolIdentifier, int offsetFromSymbolName)
        {
            string symbolAddressGlobalName = symbolIdentifier + "___SYMBOL";
            LLVMValueRef symbolAddress;
            var intType = LLVMTypeRef.Int32;
            if (s_symbolValues.TryGetValue(symbolAddressGlobalName, out symbolAddress))
            {
                var int8PtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                var intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
                var pointerToRealSymbol = LLVMValueRef.CreateConstBitCast(realSymbol, int8PtrType);
                var offsetValue = LLVMValueRef.CreateConstInt(intType, (uint)offsetFromSymbolName, false);
                var symbolPointerData = LLVMValueRef.CreateConstGEP(pointerToRealSymbol, new LLVMValueRef[] { offsetValue });
                var symbolPointerDataAsInt32Ptr = LLVMValueRef.CreateConstBitCast(symbolPointerData, intPtrType);
                symbolAddress.Initializer = symbolPointerDataAsInt32Ptr;
            }
        }

        public int EmitSymbolRef(string realSymbolName, int offsetFromSymbolName, bool isFunction, RelocType relocType, int delta = 0)
        {
            int symbolStartOffset = _currentObjectData.Count;

            // Workaround for ObjectWriter's lack of support for IMAGE_REL_BASED_RELPTR32
            // https://github.com/dotnet/corert/issues/3278
            if (relocType == RelocType.IMAGE_REL_BASED_RELPTR32)
            {
                relocType = RelocType.IMAGE_REL_BASED_REL32;
                delta = checked(delta + sizeof(int));
            }
            uint totalOffset = checked((uint)delta + unchecked((uint)offsetFromSymbolName));

            EmitBlob(new byte[this._nodeFactory.Target.PointerSize]);
            if (relocType == RelocType.IMAGE_REL_BASED_REL32)
            {
                return this._nodeFactory.Target.PointerSize;
            }
            _currentObjectSymbolRefs.Add(symbolStartOffset, new SymbolRefData(isFunction, realSymbolName, totalOffset));
            return _nodeFactory.Target.PointerSize;
        }

        public string GetMangledName(TypeDesc type)
        {
            return _nodeFactory.NameMangler.GetMangledTypeName(type);
        }

        public void BuildSymbolDefinitionMap(ObjectNode node, ISymbolDefinitionNode[] definedSymbols)
        {
            _offsetToDefName.Clear();
            foreach (ISymbolDefinitionNode n in definedSymbols)
            {
                if (!_offsetToDefName.ContainsKey(n.Offset))
                {
                    _offsetToDefName[n.Offset] = new List<ISymbolDefinitionNode>();
                }

                _offsetToDefName[n.Offset].Add(n);
                _byteInterruptionOffsets.Add(n.Offset);
            }

            var symbolNode = node as ISymbolDefinitionNode;
            if (symbolNode != null)
            {
                _sb.Clear();
                AppendExternCPrefix(_sb);
                symbolNode.AppendMangledName(_nodeFactory.NameMangler, _sb);
                _currentNodeZeroTerminatedName = _sb.Append('\0').ToUtf8String();
            }
            else
            {
                _currentNodeZeroTerminatedName = default(Utf8String);
            }
        }

        private void AppendExternCPrefix(Utf8StringBuilder sb)
        {
        }

        // Returns size of the emitted symbol reference
        public int EmitSymbolReference(ISymbolNode target, int delta, RelocType relocType)
        {
            string realSymbolName = GetBaseSymbolName(target, _nodeFactory.NameMangler, true);

            if (realSymbolName == null)
            {
                Console.WriteLine("Unable to generate symbolRef to " + target.GetMangledName(_nodeFactory.NameMangler));

                int pointerSize = _nodeFactory.Target.PointerSize;
                EmitBlob(new byte[pointerSize]);
                return pointerSize;
            }
            int offsetFromBase = GetNumericOffsetFromBaseSymbolValue(target) + target.Offset;
            return EmitSymbolRef(realSymbolName, offsetFromBase, target is WebAssemblyMethodCodeNode || target is WebAssemblyBlockRefNode, relocType, delta);
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

                    // Make sure we've gotten the correct size for the reloc
                    Debug.Assert(reloc.RelocType == RelocType.IMAGE_REL_BASED_DIR64 ||
                        reloc.RelocType == RelocType.IMAGE_REL_BASED_HIGHLOW);

                    long delta;
                    unsafe
                    {
                        fixed (void* location = &blob[i])
                        {
                            delta = Relocation.ReadValue(reloc.RelocType, location);
                        }
                    }
                    int size = EmitSymbolReference(reloc.Target, (int)delta, reloc.RelocType);

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
            List<ISymbolDefinitionNode> nodes;
            if (_offsetToDefName.TryGetValue(currentOffset, out nodes))
            {
                foreach (var name in nodes)
                {
                    _sb.Clear();
                    AppendExternCPrefix(_sb);
                    name.AppendMangledName(_nodeFactory.NameMangler, _sb);

                    string symbolId = name.GetMangledName(_nodeFactory.NameMangler);
                    int offsetFromBase = GetNumericOffsetFromBaseSymbolValue(name);
                    Debug.Assert(offsetFromBase == currentOffset);

                    _symbolDefs.Add(new KeyValuePair<string, int>(symbolId, offsetFromBase));
                    /*
                    string alternateName = _nodeFactory.GetSymbolAlternateName(name);
                    if (alternateName != null)
                    {
                        _sb.Clear();
                        //AppendExternCPrefix(_sb);
                        _sb.Append(alternateName);

                        EmitSymbolDef(_sb);
                    }*/
                }
            }
        }

        //System.IO.FileStream _file;
        string _objectFilePath;
        public WebAssemblyObjectWriter(string objectFilePath, NodeFactory factory, WebAssemblyCodegenCompilation compilation)
        {
            _nodeFactory = factory;
            _objectFilePath = objectFilePath;
            Module = compilation.Module;
            DIBuilder = compilation.DIBuilder;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public virtual void Dispose(bool bDisposing)
        {
            FinishObjWriter(Module.Context);
            //if (_file != null)
            //{
            //    // Finalize object emission.
            //    FinishObjWriter();
            //    _file.Flush();
            //    _file.Dispose();
            //    _file = null;
            //}

            _nodeFactory = null;

            if (bDisposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~WebAssemblyObjectWriter()
        {
            Dispose(false);
        }

        private bool ShouldShareSymbol(ObjectNode node)
        {
            if (_nodeFactory.CompilationModuleGroup.IsSingleFileCompilation)
                return false;

            if (!(node is ISymbolNode))
                return false;

            // These intentionally clash with one another, but are merged with linker directives so should not be Comdat folded
            if (node is ModulesSectionNode)
                return false;

            return true;
        }

        private ObjectNodeSection GetSharedSection(ObjectNodeSection section, string key)
        {
            string standardSectionPrefix = "";
            if (section.IsStandardSection)
                standardSectionPrefix = ".";

            return new ObjectNodeSection(standardSectionPrefix + section.Name, section.Type, key);
        }

        public void ResetByteRunInterruptionOffsets(Relocation[] relocs)
        {
            _byteInterruptionOffsets.Clear();

            for (int i = 0; i < relocs.Length; ++i)
            {
                _byteInterruptionOffsets.Add(relocs[i].Offset);
            }
        }

        private static int GetVTableSlotsCount(NodeFactory factory, TypeDesc type)
        {
            if (type == null)
                return 0;
            int slotsOnCurrentType = factory.VTable(type).Slots.Count;
            return slotsOnCurrentType + GetVTableSlotsCount(factory, type.BaseType);
        }

        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, NodeFactory factory, WebAssemblyCodegenCompilation compilation, IObjectDumper dumper)
        {
            WebAssemblyObjectWriter objectWriter = new WebAssemblyObjectWriter(objectFilePath, factory, compilation);
            bool succeeded = false;

            try
            {
                objectWriter.EmitReadyToRunHeaderCallback(compilation.Module.Context);
                //ObjectNodeSection managedCodeSection = null;

                var listOfOffsets = new List<int>();
                foreach (DependencyNode depNode in nodes)
                {
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;
                    
                    if (node.ShouldSkipEmittingObjectNode(factory))
                        continue;

                    if (node is ReadyToRunGenericHelperNode readyToRunGenericHelperNode)
                    {
                        objectWriter.GetCodeForReadyToRunGenericHelper(compilation, readyToRunGenericHelperNode, factory);
                        continue;
                    }

                    objectWriter.StartObjectNode(node);
                    ObjectData nodeContents = node.GetData(factory);

                    if (dumper != null)
                        dumper.DumpObjectNode(factory.NameMangler, node, nodeContents);

#if DEBUG
                    foreach (ISymbolNode definedSymbol in nodeContents.DefinedSymbols)
                    {
                        try
                        {
                            _previouslyWrittenNodeNames.Add(definedSymbol.GetMangledName(factory.NameMangler), definedSymbol);
                        }
                        catch (ArgumentException)
                        {
                            ISymbolNode alreadyWrittenSymbol = _previouslyWrittenNodeNames[definedSymbol.GetMangledName(factory.NameMangler)];
                            Debug.Fail("Duplicate node name emitted to file",
                            $"Symbol {definedSymbol.GetMangledName(factory.NameMangler)} has already been written to the output object file {objectFilePath} with symbol {alreadyWrittenSymbol}");
                        }
                    }
#endif
   
                    ObjectNodeSection section = node.Section;
                    if (objectWriter.ShouldShareSymbol(node))
                    {
                        section = objectWriter.GetSharedSection(section, ((ISymbolNode)node).GetMangledName(factory.NameMangler));
                    }

                    // Ensure section and alignment for the node.
                    objectWriter.SetSection(section);
                    objectWriter.EmitAlignment(nodeContents.Alignment);

                    objectWriter.ResetByteRunInterruptionOffsets(nodeContents.Relocs);

                    // Build symbol definition map.
                    objectWriter.BuildSymbolDefinitionMap(node, nodeContents.DefinedSymbols);

                    Relocation[] relocs = nodeContents.Relocs;
                    int nextRelocOffset = -1;
                    int nextRelocIndex = -1;
                    if (relocs.Length > 0)
                    {
                        nextRelocOffset = relocs[0].Offset;
                        nextRelocIndex = 0;
                    }

                    int i = 0;

                    listOfOffsets.Clear();
                    listOfOffsets.AddRange(objectWriter._byteInterruptionOffsets);

                    int offsetIndex = 0;
                    while (i < nodeContents.Data.Length)
                    {
                        // Emit symbol definitions if necessary
                        objectWriter.EmitSymbolDefinition(i);
                        if (i == nextRelocOffset)
                        {
                            Relocation reloc = relocs[nextRelocIndex];

                            long delta;
                            unsafe
                            {
                                fixed (void* location = &nodeContents.Data[i])
                                {
                                    delta = Relocation.ReadValue(reloc.RelocType, location);
                                }
                            }
                            ISymbolNode symbolToWrite = reloc.Target;
                            var eeTypeNode = reloc.Target as EETypeNode;
                            if (eeTypeNode != null)
                            {
                                if (eeTypeNode.ShouldSkipEmittingObjectNode(factory))
                                {
                                    symbolToWrite = factory.ConstructedTypeSymbol(eeTypeNode.Type);
                                }
                            }
                            int size = objectWriter.EmitSymbolReference(symbolToWrite, (int)delta, reloc.RelocType);

                            /*
                             WebAssembly has no thumb 
                            // Emit a copy of original Thumb2 instruction that came from RyuJIT
                            if (reloc.RelocType == RelocType.IMAGE_REL_BASED_THUMB_MOV32 ||
                                reloc.RelocType == RelocType.IMAGE_REL_BASED_THUMB_BRANCH24)
                            {
                                unsafe
                                {
                                    fixed (void* location = &nodeContents.Data[i])
                                    {
                                        objectWriter.EmitBytes((IntPtr)location, size);
                                    }
                                }
                            }*/

                            // Update nextRelocIndex/Offset
                            if (++nextRelocIndex < relocs.Length)
                            {
                                nextRelocOffset = relocs[nextRelocIndex].Offset;
                            }
                            else
                            {
                                // This is the last reloc. Set the next reloc offset to -1 in case the last reloc has a zero size, 
                                // which means the reloc does not have vacant bytes corresponding to in the data buffer. E.g, 
                                // IMAGE_REL_THUMB_BRANCH24 is a kind of 24-bit reloc whose bits scatte over the instruction that 
                                // references it. We do not vacate extra bytes in the data buffer for this kind of reloc.
                                nextRelocOffset = -1;
                            }
                            i += size;
                        }
                        else
                        {
                            while (offsetIndex < listOfOffsets.Count && listOfOffsets[offsetIndex] <= i)
                            {
                                offsetIndex++;
                            }

                            int nextOffset = offsetIndex == listOfOffsets.Count ? nodeContents.Data.Length : listOfOffsets[offsetIndex];

                            unsafe
                            {
                                // Todo: Use Span<T> instead once it's available to us in this repo
                                fixed (byte* pContents = &nodeContents.Data[i])
                                {
                                    objectWriter.EmitBytes((IntPtr)(pContents), nextOffset - i);
                                    i += nextOffset - i;
                                }
                            }

                        }
                    }
                    Debug.Assert(i == nodeContents.Data.Length);

                    // It is possible to have a symbol just after all of the data.
                    objectWriter.EmitSymbolDefinition(nodeContents.Data.Length);
                    objectWriter.DoneObjectNode();
                }

                succeeded = true;
            }
            finally
            {
                objectWriter.Dispose();

                if (!succeeded)
                {
                    // If there was an exception while generating the OBJ file, make sure we don't leave the unfinished
                    // object file around.
                    try
                    {
                        File.Delete(objectFilePath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void GetCodeForReadyToRunGenericHelper(WebAssemblyCodegenCompilation compilation, ReadyToRunGenericHelperNode node, NodeFactory factory)
        {
            LLVMBuilderRef builder = compilation.Module.Context.CreateBuilder();
            var args = new List<LLVMTypeRef>();
            MethodDesc delegateCtor = null;
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                delegateCtor = target.Constructor.Method;
                bool isStatic = delegateCtor.Signature.IsStatic;
                int argCount = delegateCtor.Signature.Length;
                if (!isStatic) argCount++;
                for (int i = 0; i < argCount; i++)
                {
                    TypeDesc argType;
                    if (i == 0 && !isStatic)
                    {
                        argType = delegateCtor.OwningType;
                    }
                    else
                    {
                        argType = delegateCtor.Signature[i - (isStatic ? 0 : 1)];
                    }
                    args.Add(ILImporter.GetLLVMTypeForTypeDesc(argType));
                }
            }

            LLVMValueRef helperFunc = Module.GetNamedFunction(node.GetMangledName(factory.NameMangler));

            if (helperFunc.Handle == IntPtr.Zero)
            {
                throw new Exception("if the function is requested here, it should have been created earlier");
            }
            var helperBlock = helperFunc.AppendBasicBlock("genericHelper");
            builder.PositionAtEnd(helperBlock);
            var importer = new ILImporter(builder, compilation, Module, helperFunc, delegateCtor);
            LLVMValueRef ctx;
            string gepName;
            if (node is ReadyToRunGenericLookupFromTypeNode)
            {
                // Locate the VTable slot that points to the dictionary
                int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(factory, (TypeDesc)node.DictionaryOwner);
            
                int pointerSize = factory.Target.PointerSize;
                // Load the dictionary pointer from the VTable
                int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);
                var slotGep = builder.BuildGEP(helperFunc.GetParam(1), new[] {LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)slotOffset, false)}, "slotGep");
                var slotGepPtrPtr = builder.BuildPointerCast(slotGep,
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "slotGepPtrPtr");
                ctx = builder.BuildLoad(slotGepPtrPtr, "dictGep");
                gepName = "typeNodeGep";
            }
            else
            {
                ctx = helperFunc.GetParam(1);
                gepName = "paramGep";
            }

            LLVMValueRef resVar = OutputCodeForDictionaryLookup(builder, factory, node, node.LookupSignature, ctx, gepName);
            
            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;
            
                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            importer.OutputCodeForTriggerCctor(target, resVar);
                        }
                    }
                    break;
            
                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        var ptrPtrPtr = builder.BuildBitCast(resVar, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), 0), "ptrPtrPtr");

                        resVar = builder.BuildLoad(builder.BuildLoad(ptrPtrPtr, "ind1"), "ind2");
            
                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            var nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcRegionLookup, ctx, "lazyGep");
                            importer.OutputCodeForTriggerCctor(target, nonGcStaticsBase);
                        }
                    }
                    break;
            
                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;
            
                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            var threadStaticBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcRegionLookup, ctx, "tsGep");
                            importer.OutputCodeForTriggerCctor(target, threadStaticBase);
                        }
                        resVar = importer.OutputCodeForGetThreadStaticBaseForType(resVar).ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), builder);
                    }
                    break;
            
                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                        MethodDesc constructor = target.Constructor.Method;
                        var fatPtr = ILImporter.MakeFatPointer(builder, resVar, compilation);
                        importer.OutputCodeForDelegateCtorInit(builder, helperFunc, constructor, fatPtr);
                    }
                    break;
            
                // These are all simple: just get the thing from the dictionary and we're done
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.TypeHandleForCasting:
                case ReadyToRunHelperId.MethodHandle:
                case ReadyToRunHelperId.FieldHandle:
                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.MethodEntry:
                case ReadyToRunHelperId.VirtualDispatchCell:
                case ReadyToRunHelperId.DefaultConstructor:
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (node.Id != ReadyToRunHelperId.DelegateCtor)
            {
                builder.BuildRet(resVar);
            }
             else
            {
                builder.BuildRetVoid();
            }
        }

        private LLVMValueRef OutputCodeForDictionaryLookup(LLVMBuilderRef builder, NodeFactory factory,
            ReadyToRunGenericHelperNode node, GenericLookupResult lookup, LLVMValueRef ctx, string gepName)
        {
            // Find the generic dictionary slot
            int dictionarySlot = factory.GenericDictionaryLayout(node.DictionaryOwner).GetSlotForEntry(lookup);
            int offset = dictionarySlot * factory.Target.PointerSize;

            // Load the generic dictionary cell
            LLVMValueRef retGep = builder.BuildGEP(ctx, new[] {LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)offset, false)}, "retGep");
            LLVMValueRef castGep = builder.BuildBitCast(retGep, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");
            LLVMValueRef retRef = builder.BuildLoad(castGep, gepName);

            switch (lookup.LookupResultReferenceType(factory))
            {
                case GenericLookupResultReferenceType.Indirect:
                    var ptrPtr = builder.BuildBitCast(retRef, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");
                    retRef = builder.BuildLoad(ptrPtr, "indLoad");
                    break;

                case GenericLookupResultReferenceType.ConditionalIndirect:
                    throw new NotImplementedException();

                default:
                    break;
            }

            return retRef;
        }

    }
}

namespace Internal.IL
{
    partial class ILImporter
    {
        public ILImporter(LLVMBuilderRef builder, WebAssemblyCodegenCompilation compilation, LLVMModuleRef module, LLVMValueRef helperFunc, MethodDesc delegateCtor)
        {
            this._builder = builder;
            this._compilation = compilation;
            this.Module = module;
            this._currentFunclet = helperFunc;
            _locals = new LocalVariableDefinition[0];
            if (delegateCtor == null)
            {
                _signature = new MethodSignature(MethodSignatureFlags.None, 0, GetWellKnownType(WellKnownType.Void),
                    new TypeDesc[0]);
            }
            else
            {
                _signature = delegateCtor.Signature;
                _argSlots = new LLVMValueRef[_signature.Length];
                int signatureIndex = 2; // past hidden param
                int thisOffset = 0;
                if (!_signature.IsStatic)
                {
                    thisOffset = 1;
                }
                for (int i = 0; i < _signature.Length; i++)
                {
                    if (CanStoreTypeOnStack(_signature[i]))
                    {
                        LLVMValueRef storageAddr;
                        LLVMValueRef argValue = helperFunc.GetParam((uint)signatureIndex);

                        // The caller will always pass the argument on the stack. If this function doesn't have 
                        // EH, we can put it in an alloca for efficiency and better debugging. Otherwise,
                        // copy it to the shadow stack so funclets can find it
                        int argOffset = i + thisOffset;
                        string argName = $"arg{argOffset}_";
                        storageAddr = _builder.BuildAlloca(GetLLVMTypeForTypeDesc(_signature[i]), argName);
                        _argSlots[i] = storageAddr;
                        _builder.BuildStore(argValue, storageAddr);
                        signatureIndex++;
                    }
                }
            }
            _thisType = GetWellKnownType(WellKnownType.Void);
            _pointerSize = compilation.NodeFactory.Target.PointerSize;
            _exceptionRegions = new ExceptionRegion[0];
        }

        internal void OutputCodeForTriggerCctor(TypeDesc type, LLVMValueRef staticBaseValueRef)
        {
            IMethodNode helperNode = (IMethodNode)_compilation.NodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);
            TriggerCctor((MetadataType)helperNode.Method.OwningType, staticBaseValueRef, helperNode.Method.Name);
        }

        public void OutputCodeForDelegateCtorInit(LLVMBuilderRef builder, LLVMValueRef helperFunc,
            MethodDesc constructor,
            LLVMValueRef fatFunction)
        {
            StackEntry[] argValues = new StackEntry [constructor.Signature.Length + 1]; // for delegate this
            var shadowStack = helperFunc.GetParam(0);
            argValues[0] = new LoadExpressionEntry(StackValueKind.ObjRef, "this", shadowStack, GetWellKnownType(WellKnownType.Object));
            for (var i = 0; i < constructor.Signature.Length; i++)
            {
                if (i == 1)
                {
                    argValues[i + 1] = new ExpressionEntry(StackValueKind.Int32, "arg" + (i + 1),
                        builder.BuildIntToPtr(fatFunction, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "toPtr"), 
                        GetWellKnownType(WellKnownType.IntPtr));
                }
                else
                {
                    var argRef = LoadVarAddress(i + 1, LocalVarKind.Argument, out TypeDesc type);
                    var ptrPtr = builder.BuildPointerCast(argRef,
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");
                    var loadArg = builder.BuildLoad(ptrPtr, "arg" + (i + 1));
                    argValues[i + 1] = new ExpressionEntry(GetStackValueKind(constructor.Signature[i]), "arg" + i + 1, loadArg,
                        constructor.Signature[i]);
                }
            }
            HandleCall(constructor, constructor.Signature, constructor, argValues, null);
        }
    }
}
