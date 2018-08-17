// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.TypesDebugInfo;
using Internal.JitInterface;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

using LLVMSharp;
using ILCompiler.CodeGen;
using System.Linq;
using Internal.IL;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer using https://github.com/dotnet/llilc
    /// </summary>
    internal class WebAssemblyObjectWriter : IDisposable
    {
        public static string GetBaseSymbolName(ISymbolNode symbol, NameMangler nameMangler, bool objectWriterUse = false)
        {
            if (symbol is WebAssemblyMethodCodeNode)
            {
                return symbol.GetMangledName(nameMangler);
            }

            if (symbol is ObjectNode)
            {
                ObjectNode objNode = (ObjectNode)symbol;
                ISymbolDefinitionNode symbolDefNode = (ISymbolDefinitionNode)symbol;
                if (symbolDefNode.Offset == 0)
                {
                    return symbol.GetMangledName(nameMangler);
                }
                else
                {
                    return symbol.GetMangledName(nameMangler) + "___REALBASE";
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

        private static Dictionary<string, LLVMValueRef> s_symbolValues = new Dictionary<string, LLVMValueRef>();
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
            var intPtrType = LLVM.PointerType(LLVM.Int32Type(), 0);
            var myGlobal = LLVM.AddGlobalInAddressSpace(module, intPtrType, symbolAddressGlobalName, 0);
            LLVM.SetGlobalConstant(myGlobal, (LLVMBool)true);
            LLVM.SetLinkage(myGlobal, LLVMLinkage.LLVMInternalLinkage);
            s_symbolValues.Add(symbolAddressGlobalName, myGlobal);
            return myGlobal;
        }

        private static int GetNumericOffsetFromBaseSymbolValue(ISymbolNode symbol)
        {
            if (symbol is WebAssemblyMethodCodeNode)
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

        public void FinishObjWriter()
        {
            // Since emission to llvm is delayed until after all nodes are emitted... emit now.
            foreach (var nodeData in _dataToFill)
            {
                nodeData.Fill(Module, _nodeFactory);
            }

            EmitNativeMain();

            EmitDebugMetadata();

            LLVM.WriteBitcodeToFile(Module, _objectFilePath);
#if DEBUG
            LLVM.PrintModuleToFile(Module, Path.ChangeExtension(_objectFilePath, ".txt"), out string unused2);
#endif //DEBUG
            LLVM.VerifyModule(Module, LLVMVerifierFailureAction.LLVMAbortProcessAction, out string unused);

            //throw new NotImplementedException(); // This function isn't complete
        }

        private void EmitDebugMetadata()
        {
            var dwarfVersion = LLVM.MDNode(new[]
            {
                LLVM.ConstInt(LLVM.Int32Type(), 2, false),
                LLVM.MDString("Dwarf Version", 13),
                LLVM.ConstInt(LLVM.Int32Type(), 4, false)
            });
            var dwarfSchemaVersion = LLVM.MDNode(new[]
            {
                LLVM.ConstInt(LLVM.Int32Type(), 2, false),
                LLVM.MDString("Debug Info Version", 18),
                LLVM.ConstInt(LLVM.Int32Type(), 3, false)
            });
            LLVM.AddNamedMetadataOperand(Module, "llvm.module.flags", dwarfVersion);
            LLVM.AddNamedMetadataOperand(Module, "llvm.module.flags", dwarfSchemaVersion);
            LLVM.DIBuilderFinalize(DIBuilder);
        }

        public static LLVMValueRef GetConstZeroArray(int length)
        {
            var int8Type = LLVM.Int8Type();
            var result = new LLVMValueRef[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = LLVM.ConstInt(int8Type, 0, LLVMMisc.False);
            }
            return LLVM.ConstArray(int8Type, result);
        }

        public static LLVMValueRef EmitGlobal(LLVMModuleRef module, FieldDesc field, NameMangler nameMangler)
        {
            if (field.IsStatic)
            {
                if (s_staticFieldMapping.TryGetValue(field, out LLVMValueRef existingValue))
                    return existingValue;
                else
                {
                    var valueType = LLVM.ArrayType(LLVM.Int8Type(), (uint)field.FieldType.GetElementSize().AsInt);
                    var llvmValue = LLVM.AddGlobal(module, valueType, nameMangler.GetMangledFieldName(field).ToString());
                    LLVM.SetLinkage(llvmValue, LLVMLinkage.LLVMInternalLinkage);
                    LLVM.SetInitializer(llvmValue, GetConstZeroArray(field.FieldType.GetElementSize().AsInt));
                    if (field.IsThreadStatic)
                    {
                        LLVM.SetThreadLocal(llvmValue, LLVMMisc.True);
                    }
                    s_staticFieldMapping.Add(field, llvmValue);
                    return llvmValue;
                }
            }
            else
                throw new NotImplementedException();
        }

        private void EmitReadyToRunHeaderCallback()
        {
            LLVMTypeRef intPtr = LLVM.PointerType(LLVM.Int32Type(), 0);
            LLVMTypeRef intPtrPtr = LLVM.PointerType(intPtr, 0);
            var callback = LLVM.AddFunction(Module, "RtRHeaderWrapper", LLVM.FunctionType(intPtrPtr, new LLVMTypeRef[0], false));
            var builder = LLVM.CreateBuilder();
            var block = LLVM.AppendBasicBlock(callback, "Block");
            LLVM.PositionBuilderAtEnd(builder, block);

            LLVMValueRef rtrHeaderPtr = GetSymbolValuePointer(Module, _nodeFactory.ReadyToRunHeader, _nodeFactory.NameMangler, false);
            LLVMValueRef castRtrHeaderPtr = LLVM.BuildPointerCast(builder, rtrHeaderPtr, intPtrPtr, "castRtrHeaderPtr");
            LLVM.BuildRet(builder, castRtrHeaderPtr);
        }

        private void EmitNativeMain()
        {
            LLVMValueRef shadowStackTop = LLVM.GetNamedGlobal(Module, "t_pShadowStackTop");

            LLVMBuilderRef builder = LLVM.CreateBuilder();
            var mainSignature = LLVM.FunctionType(LLVM.Int32Type(), new LLVMTypeRef[] { LLVM.Int32Type(), LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
            var mainFunc = LLVM.AddFunction(Module, "__managed__Main", mainSignature);
            var mainEntryBlock = LLVM.AppendBasicBlock(mainFunc, "entry");
            LLVM.PositionBuilderAtEnd(builder, mainEntryBlock);
            LLVMValueRef managedMain = LLVM.GetNamedFunction(Module, "StartupCodeMain");
            if (managedMain.Pointer == IntPtr.Zero)
            {
                throw new Exception("Main not found");
            }

            LLVMTypeRef reversePInvokeFrameType = LLVM.StructType(new LLVMTypeRef[] { LLVM.PointerType(LLVM.Int8Type(), 0), LLVM.PointerType(LLVM.Int8Type(), 0) }, false);
            LLVMValueRef reversePinvokeFrame = LLVM.BuildAlloca(builder, reversePInvokeFrameType, "ReversePInvokeFrame");
            LLVMValueRef RhpReversePInvoke2 = LLVM.GetNamedFunction(Module, "RhpReversePInvoke2");

            if (RhpReversePInvoke2.Pointer == IntPtr.Zero)
            {
                RhpReversePInvoke2 = LLVM.AddFunction(Module, "RhpReversePInvoke2", LLVM.FunctionType(LLVM.VoidType(), new LLVMTypeRef[] { LLVM.PointerType(reversePInvokeFrameType, 0) }, false));
            }

            LLVM.BuildCall(builder, RhpReversePInvoke2, new LLVMValueRef[] { reversePinvokeFrame }, "");

            var shadowStack = LLVM.BuildMalloc(builder, LLVM.ArrayType(LLVM.Int8Type(), 1000000), String.Empty);
            var castShadowStack = LLVM.BuildPointerCast(builder, shadowStack, LLVM.PointerType(LLVM.Int8Type(), 0), String.Empty);
            LLVM.BuildStore(builder, castShadowStack, shadowStackTop);

            // Pass on main arguments
            LLVMValueRef argc = LLVM.GetParam(mainFunc, 0);
            LLVMValueRef argv = LLVM.GetParam(mainFunc, 1);

            LLVMValueRef mainReturn = LLVM.BuildCall(builder, managedMain, new LLVMValueRef[]
            {
                castShadowStack,
                argc,
                argv,
            },
            "returnValue");

            LLVM.BuildRet(builder, mainReturn);
            LLVM.SetLinkage(mainFunc, LLVMLinkage.LLVMExternalLinkage);
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
            public SymbolRefData(bool isFunction, string symbolName, int offset)
            {
                IsFunction = isFunction;
                SymbolName = symbolName;
                Offset = offset;
            }

            readonly bool IsFunction;
            readonly string SymbolName;
            readonly int Offset;

            public LLVMValueRef ToLLVMValueRef(LLVMModuleRef module)
            {
                LLVMValueRef valRef = IsFunction ? LLVM.GetNamedFunction(module, SymbolName) : LLVM.GetNamedGlobal(module, SymbolName);

                if (Offset != 0 && valRef.Pointer != IntPtr.Zero)
                {
                    var pointerType = LLVM.PointerType(LLVM.Int8Type(), 0);
                    var bitCast = LLVM.ConstBitCast(valRef, pointerType);
                    LLVMValueRef[] index = new LLVMValueRef[] {LLVM.ConstInt(LLVM.Int32Type(), (uint)Offset, (LLVMBool)false)};
                    valRef = LLVM.ConstGEP(bitCast, index);
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
                var intPtrType = LLVM.PointerType(LLVM.Int32Type(), 0);
                var intType = LLVM.Int32Type();

                var int8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);

                for (int i = 0; i < countOfPointerSizedElements; i++)
                {
                    int curOffset = (i * pointerSize);
                    SymbolRefData symbolRef;
                    if (ObjectSymbolRefs.TryGetValue(curOffset, out symbolRef))
                    {
                        LLVMValueRef pointedAtValue = symbolRef.ToLLVMValueRef(module);
                        //TODO: why did this come back null
                        if (pointedAtValue.Pointer != IntPtr.Zero)
                        {
                            var ptrValue = LLVM.ConstBitCast(pointedAtValue, intPtrType);
                            entries.Add(ptrValue);
                        }
                        else
                        {
                            entries.Add(LLVM.ConstPointerNull(intPtrType));
                        }
                    }
                    else
                    {
                        int value = BitConverter.ToInt32(currentObjectData, curOffset);
                        var nullptr = LLVM.ConstPointerNull(int8PtrType);
                        var dataVal = LLVM.ConstInt(intType, (uint)value, (LLVMBool)false);
                        var ptrValAsInt8Ptr = LLVM.ConstGEP(nullptr, new LLVMValueRef[] { dataVal });

                        var ptrValue = LLVM.ConstBitCast(ptrValAsInt8Ptr, intPtrType);
                        entries.Add(ptrValue);
                    }
                }

                var funcptrarray = LLVM.ConstArray(intPtrType, entries.ToArray());
                LLVM.SetInitializer(Node, funcptrarray);
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
            int pointerSize = _nodeFactory.Target.PointerSize;
            EmitAlignment(_nodeFactory.Target.PointerSize);
            Debug.Assert(_nodeFactory.Target.PointerSize == 4);
            int countOfPointerSizedElements = _currentObjectData.Count / _nodeFactory.Target.PointerSize;

            ISymbolNode symNode = _currentObjectNode as ISymbolNode;
            if (symNode == null)
                symNode = ((IHasStartSymbol)_currentObjectNode).StartSymbol;
            string realName = GetBaseSymbolName(symNode, _nodeFactory.NameMangler, true);

            var intPtrType = LLVM.PointerType(LLVM.Int32Type(), 0);
            var arrayglobal = LLVM.AddGlobalInAddressSpace(Module, LLVM.ArrayType(intPtrType, (uint)countOfPointerSizedElements), realName, 0);
            LLVM.SetLinkage(arrayglobal, LLVMLinkage.LLVMExternalLinkage);

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
            var intType = LLVM.Int32Type();
            if (s_symbolValues.TryGetValue(symbolAddressGlobalName, out symbolAddress))
            {
                var int8PtrType = LLVM.PointerType(LLVM.Int8Type(), 0);
                var intPtrType = LLVM.PointerType(LLVM.Int32Type(), 0);
                var pointerToRealSymbol = LLVM.ConstBitCast(realSymbol, int8PtrType);
                var offsetValue = LLVM.ConstInt(intType, (uint)offsetFromSymbolName, (LLVMBool)false);
                var symbolPointerData = LLVM.ConstGEP(pointerToRealSymbol, new LLVMValueRef[] { offsetValue });
                var symbolPointerDataAsInt32Ptr = LLVM.ConstBitCast(symbolPointerData, intPtrType);
                LLVM.SetInitializer(symbolAddress, symbolPointerDataAsInt32Ptr);
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

            int totalOffset = checked(delta + offsetFromSymbolName);

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
            int offsetFromBase = GetNumericOffsetFromBaseSymbolValue(target);
            return EmitSymbolRef(realSymbolName, offsetFromBase, target is WebAssemblyMethodCodeNode, relocType, delta);
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
            FinishObjWriter();
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
                objectWriter.EmitReadyToRunHeaderCallback();
                //ObjectNodeSection managedCodeSection = null;

                var listOfOffsets = new List<int>();
                foreach (DependencyNode depNode in nodes)
                {
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(factory))
                        continue;

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
                            int size = objectWriter.EmitSymbolReference(reloc.Target, (int)delta, reloc.RelocType);

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
    }
}
