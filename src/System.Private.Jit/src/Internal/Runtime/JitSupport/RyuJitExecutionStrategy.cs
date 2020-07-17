// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Internal.JitInterface;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;

using ILCompiler;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.JitSupport
{
    public class RyuJitExecutionStrategy : MethodExecutionStrategy
    {
        private const string NativeJitSupportLibrary = "*";

        private CorInfoImpl _corInfoImpl;
        private TypeSystemContext _context;
        private NodeFactory _nodeFactory;

        private void UpdateBytesUsed(ObjectNode.ObjectData nodeData, ref int bytesUsed)
        {
            bytesUsed = bytesUsed.AlignUp(nodeData.Alignment);
            bytesUsed += nodeData.Data.Length;
            return;
        }

        [DllImport(NativeJitSupportLibrary)]
        static extern IntPtr AllocJittedCode(UInt32 cbCode, UInt32 align, out IntPtr pCodeManager);

        [DllImport(NativeJitSupportLibrary)]
        static extern void SetEHInfoPtr(IntPtr pCodeManager, IntPtr pbCode, IntPtr ehInfo);

        [DllImport(NativeJitSupportLibrary)]
        static extern unsafe IntPtr PublishRuntimeFunction(
            IntPtr pCodeManager,
            IntPtr pbCode,
            IntPtr pMainRuntimeFunction,
            UInt32 startOffset,
            UInt32 endOffset,
            byte[] pUnwindInfo,
            UInt32 cbUnwindInfo,
            byte[] pGCData,
            UInt32 cbGCData);

        [DllImport(NativeJitSupportLibrary)]
        static extern void UpdateRuntimeFunctionTable(IntPtr pCodeManager);

        [DllImport(NativeJitSupportLibrary)]
        static extern void InitJitCodeManager(IntPtr mrtModule);

        public override IntPtr OnEntryPoint(MethodEntrypointPtr methodEntrypoint, IntPtr callerArgs)
        {
            lock (this)
            {
                if (_corInfoImpl == null)
                {
                    InitJitCodeManager(RuntimeAugments.RhGetOSModuleForMrt ());

                    // TODO: Recycle jit interface object and TypeSystemContext
                    _context = TypeSystemContextFactory.Create();

                    Compilation compilation = new Compilation(_context);
                    _nodeFactory = compilation.NodeFactory;

                    JitConfigProvider.Initialize(new CorJitFlag[] { CorJitFlag.CORJIT_FLAG_DEBUG_CODE }, Array.Empty<KeyValuePair<string, string>>());

                    _corInfoImpl = new CorInfoImpl(compilation);
                }

                MethodDesc methodToCompile = methodEntrypoint.MethodIdentifier.ToMethodDesc(_context);

                JitMethodCodeNode codeNode = new JitMethodCodeNode(methodToCompile);
                _corInfoImpl.CompileMethod(codeNode);

                ObjectNode.ObjectData codeData = codeNode.GetData(null, false);

                List<ObjectNode> nodesToEmit = new List<ObjectNode>();
                Dictionary<DependencyNodeCore<NodeFactory>, object> relocTargets = new Dictionary<DependencyNodeCore<NodeFactory>, object>();
                int totalAllocSizeNeeded = 0;
                int nonObjectRelocTargets = 0;

                nodesToEmit.Add(codeNode);
                UpdateBytesUsed(codeNode.GetData(_nodeFactory), ref totalAllocSizeNeeded);

                int offsetOfEHData = totalAllocSizeNeeded;

                if (codeNode.EHInfo != null)
                {
                    Debug.Assert(codeNode.EHInfo.Alignment == 1); // Assert needed as otherwise offsetOfEHData will be wrong

                    UpdateBytesUsed(codeNode.EHInfo, ref totalAllocSizeNeeded);
                    ComputeDependencySizeAndRelocData(codeNode.EHInfo, relocTargets, nodesToEmit, ref totalAllocSizeNeeded, ref nonObjectRelocTargets);
                }

                for (int i = 0; i < nodesToEmit.Count; i++)
                {
                    ObjectNode objNode = nodesToEmit[i];
                    ComputeDependencySizeAndRelocData(objNode.GetData(_nodeFactory, true), relocTargets, nodesToEmit, ref totalAllocSizeNeeded, ref nonObjectRelocTargets);
                }

                if (nonObjectRelocTargets != 0)
                {
                    totalAllocSizeNeeded = totalAllocSizeNeeded.AlignUp(IntPtr.Size);
                }

                int relocTargetOffsetStart = totalAllocSizeNeeded;

                DependencyNodeCore<NodeFactory>[] relocTargetsArray = new DependencyNodeCore<NodeFactory>[nonObjectRelocTargets];
                {
                    int iRelocTarget = 0;
                    foreach (var relocTarget in relocTargets)
                    {
                        if (!(relocTarget.Key is ObjectNode))
                        {
                            relocTargetsArray[iRelocTarget] = relocTarget.Key;
                            totalAllocSizeNeeded += IntPtr.Size;
                            iRelocTarget++;
                        }
                    }
                    Debug.Assert(iRelocTarget == nonObjectRelocTargets);
                }

                GenericDictionaryCell[] genDictCells = new GenericDictionaryCell[relocTargetsArray.Length];
                for (int iRelocTarget = 0; iRelocTarget < relocTargetsArray.Length; iRelocTarget++)
                {
                    DependencyNodeCore<NodeFactory> relocTarget = relocTargetsArray[iRelocTarget];
                    GenericDictionaryCell newCell = null;

                    if (relocTarget is ExternObjectSymbolNode)
                    {
                        var externObjectSymbolNode = (ExternObjectSymbolNode)relocTarget;
                        var newMethodCell = externObjectSymbolNode.GetDictionaryCell();
                        newCell = newMethodCell;
                    }

                    if (newCell == null)
                    {
                        Environment.FailFast("Unknown reloc target type");
                    }
                    genDictCells[iRelocTarget] = newCell;
                }

                IntPtr[] relocTargetsAsIntPtr = null;

                TypeLoaderEnvironment.Instance.RunUnderTypeLoaderLock(
                    () =>
                    {
                        TypeBuilderApi.ResolveMultipleCells(genDictCells, out relocTargetsAsIntPtr);
                    });

                // Layout of allocated memory...
                // ObjectNodes (aligned as appropriate)
                IntPtr pCodeManager;
                IntPtr jittedCode = AllocJittedCode(checked((uint)totalAllocSizeNeeded), 8/* TODO, alignment calculation */, out pCodeManager);
                int currentOffset = 0;

                foreach (var node in nodesToEmit)
                {
                    ObjectNode.ObjectData objectData = node.GetData(_nodeFactory);
                    EmitAndRelocData(objectData, jittedCode, relocTargetOffsetStart, ref currentOffset, relocTargetsArray, relocTargetsAsIntPtr);

                    // EHInfo doesn't get its own node, but it does get emitted into the stream.
                    if ((node == codeNode) && (codeNode.EHInfo != null))
                    {
                        Debug.Assert(offsetOfEHData == currentOffset);
                        EmitAndRelocData(codeNode.EHInfo, jittedCode, relocTargetOffsetStart, ref currentOffset, relocTargetsArray, relocTargetsAsIntPtr);
                    }
                }

                foreach (IntPtr ptr in relocTargetsAsIntPtr)
                {
                    currentOffset = currentOffset.AlignUp(IntPtr.Size);
                    Marshal.WriteIntPtr(jittedCode, currentOffset, ptr);
                    currentOffset += IntPtr.Size;
                }

                SetEHInfoPtr(pCodeManager, jittedCode, jittedCode + offsetOfEHData);

                IntPtr mainRuntimeFunction = IntPtr.Zero;

                for (int i = 0; i < codeNode.FrameInfos.Length; i++)
                {
                    FrameInfo frame = codeNode.FrameInfos[i];
                    byte[] frameData = frame.BlobData;
                    byte[] gcInfoData = Array.Empty<byte>();
                    byte[] gcInfoDataDeref = frameData;

                    if (i == 0)
                    {
                        // For main function, add the gc info to the data
                        gcInfoDataDeref = gcInfoData = codeNode.GCInfo;
                    }

                    IntPtr publishedFunction = PublishRuntimeFunction(pCodeManager,
                                    jittedCode,
                                    mainRuntimeFunction,
                                    checked((uint)frame.StartOffset),
                                    checked((uint)frame.EndOffset),
                                    frameData,
                                    checked((uint)frameData.Length),
                                    gcInfoDataDeref,
                                    checked((uint)gcInfoData.Length));

                    if (i == 0)
                    {
                        mainRuntimeFunction = publishedFunction;
                    }
                }

                if (mainRuntimeFunction != IntPtr.Zero)
                {
                    UpdateRuntimeFunctionTable(pCodeManager);
                }

                methodEntrypoint.MethodCode = jittedCode;

                return jittedCode;
            }
        }

        void ComputeDependencySizeAndRelocData(ObjectNode.ObjectData objectData, Dictionary<DependencyNodeCore<NodeFactory>, object> relocTargets, List<ObjectNode> nodesToEmit, ref int totalAllocSizeNeeded, ref int nonObjectRelocTargets)
        {
            foreach (var reloc in objectData.Relocs)
            {
                DependencyNodeCore<NodeFactory> relocTargetAsNode = (DependencyNodeCore<NodeFactory>)reloc.Target;

                if (!relocTargets.ContainsKey(relocTargetAsNode))
                {
                    relocTargets.Add(relocTargetAsNode, null);
                    ObjectNode relocTargetObjectNode = relocTargetAsNode as ObjectNode;
                    if (relocTargetObjectNode != null)
                    {
                        UpdateBytesUsed(relocTargetObjectNode.GetData(_nodeFactory), ref totalAllocSizeNeeded);
                        nodesToEmit.Add(relocTargetObjectNode);
                    }
                    else
                    {
                        nonObjectRelocTargets++;
                    }
                }
            }
        }

        void EmitAndRelocData(ObjectNode.ObjectData objectData, IntPtr jittedCode, int relocTargetOffsetStart, ref int currentOffset, DependencyNodeCore<NodeFactory>[] relocTargetsArray, IntPtr[] relocTargetsAsIntPtr)
        {
            currentOffset = currentOffset.AlignUp(objectData.Alignment);
            Marshal.Copy(objectData.Data, 0, jittedCode + currentOffset, objectData.Data.Length);
            foreach (Relocation reloc in objectData.Relocs)
            {
                switch (reloc.RelocType)
                {
                    case RelocType.IMAGE_REL_BASED_REL32:
                        // 4 byte offset from current pointer to target
                        // ADD ROUTINE TO FIND Relocation
                        for (int i = 0; i < relocTargetsArray.Length; i++)
                        {
                            if (relocTargetsArray[i] == reloc.Target)
                            {
                                int relocTargetAddressOffset = relocTargetOffsetStart + i * IntPtr.Size;
                                int relocAddressOffset = currentOffset + reloc.Offset;

                                int relocTargetAddressDelta = relocTargetAddressOffset - relocAddressOffset - 4;
                                Marshal.WriteInt32(jittedCode, relocAddressOffset, relocTargetAddressDelta);
                                break;
                            }
                        }
                        break;

                    default:
                        Environment.FailFast("Unknown RelocType");
                        break;
                }
            }

            currentOffset += objectData.Data.Length;
        }
    }
}
