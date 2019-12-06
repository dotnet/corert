// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Internal.TypeSystem;
using LLVMSharp.Interop;

namespace ILCompiler.WebAssembly
{
//    internal unsafe class LLVMUnsafeDIFunctions
//    {
//        public static LLVMMetadataRef DIBuilderCreateFile(LLVMDIBuilderRef builder, string filename, string directory)
//        {
//            byte[] filenameBytes = Encoding.ASCII.GetBytes(filename);
//            byte[] directoryBytes = Encoding.ASCII.GetBytes(directory);
//            fixed (byte* pFilename = filenameBytes)
//            fixed (byte* pDirectory = directoryBytes)
//            {
//                sbyte* filenameSBytePtr = (sbyte*)pFilename;
//                sbyte* directorySBytePtr = (sbyte*)pDirectory;
//                uint filenameLength = (uint)filenameBytes.Length;
//                uint directoryLength = (uint)directoryBytes.Length;
//                LLVMOpaqueMetadata* metadataPtr = LLVM.DIBuilderCreateFile((LLVMOpaqueDIBuilder*)builder.Pointer, filenameSBytePtr,
//                    (UIntPtr)(filenameLength), directorySBytePtr, (UIntPtr)(directoryLength));
//                return new LLVMMetadataRef((IntPtr)metadataPtr);
//            }
//        }
//
//        public static LLVMMetadataRef DIBuilderCreateCompileUnit(LLVMDIBuilderRef builder, LLVMDWARFSourceLanguage lang,
//            LLVMMetadataRef fileMetadataRef, string producer, int isOptimized, string flags, uint runtimeVersion,
//            string splitName, LLVMDWARFEmissionKind dwarfEmissionKind, uint dWOld, int splitDebugInlining,
//            int debugInfoForProfiling)
//        {
//            byte[] producerBytes = Encoding.ASCII.GetBytes(producer);
//            byte[] flagsBytes = Encoding.ASCII.GetBytes(flags);
//            byte[] splitNameBytes = Encoding.ASCII.GetBytes(splitName);
//
//            fixed (byte* pProducer = producerBytes)
//            fixed (byte* pFlags = flagsBytes)
//            fixed (byte* pSplitName = splitNameBytes)
//            {
//                sbyte* producerSBytePtr = (sbyte*)pProducer;
//                sbyte* flagsSBytePtr = (sbyte*)pFlags;
//                sbyte* splitNameSBytePtr = (sbyte*)pSplitName;
//                uint producerLength = (uint)producerBytes.Length;
//                uint flagsLength = (uint)flagsBytes.Length;
//                uint splitNameLength = (uint)splitNameBytes.Length;
//                LLVMOpaqueMetadata* metadataPtr = LLVM.DIBuilderCreateCompileUnit((LLVMOpaqueDIBuilder*)builder.Pointer,
//                    lang,
//                    (LLVMOpaqueMetadata*)fileMetadataRef.Pointer, producerSBytePtr, (UIntPtr)(producerLength),
//                    isOptimized, flagsSBytePtr, (UIntPtr)(flagsLength), runtimeVersion,
//                    splitNameSBytePtr, (UIntPtr)(splitNameLength), dwarfEmissionKind, dWOld, splitDebugInlining,
//                    debugInfoForProfiling);
//                return new LLVMMetadataRef((IntPtr)metadataPtr);
//            }
//        }
//
//        public static void AddNamedMetadataOperand(LLVMContextRef context, LLVMModuleRef module, string name, LLVMMetadataRef compileUnitMetadata)
//        {
//            module.AddNamedMetadataOperand(name, MetadataAsOpaqueValue(context, compileUnitMetadata));
//        }
//
//        static LLVMOpaqueValue* MetadataAsOpaqueValue(LLVMContextRef context, LLVMMetadataRef metadata)
//        {
//            return LLVM.MetadataAsValue(context, metadata);
//        }
//
//        public static LLVMValueRef MetadataAsValue(LLVMContextRef context, LLVMMetadataRef metadata)
//        {
//            return new LLVMValueRef((IntPtr)MetadataAsOpaqueValue(context, metadata));
//        }
//
//        public static LLVMMetadataRef DIBuilderCreateFunction(LLVMDIBuilderRef builder, LLVMMetadataRef scope, string methodName, string linkageName, LLVMMetadataRef debugMetadataFile, uint lineNumber, LLVMMetadataRef typeMetadata, int isLocalToUnit,
//            int isDefinition, uint scopeLine, LLVMDIFlags llvmDiFlags, int optimized)
//        {
//            byte[] methodNameBytes = Encoding.ASCII.GetBytes(methodName);
//            byte[] linkageNameBytes = Encoding.ASCII.GetBytes(linkageName);
//            fixed (byte* pMethodName = methodNameBytes)
//            fixed (byte* pLinkageNameBytes = linkageNameBytes)
//            {
//                sbyte* methodNameSBytePtr = (sbyte*)pMethodName;
//                sbyte* linkageNameSBytePtr = (sbyte*)pLinkageNameBytes;
//
//                uint methodNameLength = (uint)methodNameBytes.Length;
//                uint linkageNameLength = (uint)linkageNameBytes.Length;
//                return LLVM.DIBuilderCreateFunction((LLVMOpaqueDIBuilder*)builder.Pointer, (LLVMOpaqueMetadata*)scope.Pointer, methodNameSBytePtr, (UIntPtr)(&methodNameLength), linkageNameSBytePtr, (UIntPtr)(&linkageNameLength),
//                    (LLVMOpaqueMetadata*)debugMetadataFile.Pointer, lineNumber, (LLVMOpaqueMetadata*)typeMetadata.Pointer, isLocalToUnit, isDefinition, scopeLine, llvmDiFlags, optimized);
//            }
//        }
//
//        public static LLVMMetadataRef CreateDebugLocation(LLVMContextRef context, uint lineNumber, uint column, LLVMMetadataRef debugFunction, LLVMMetadataRef inlinedAt)
//        {
//            return LLVM.DIBuilderCreateDebugLocation((LLVMOpaqueContext*)context.Pointer, lineNumber, column, debugFunction, inlinedAt);
//        }
//
//        public static void DIBuilderFinalize(LLVMDIBuilderRef builder)
//        {
//            LLVM.DIBuilderFinalize(builder);
//        }
//
//        public static LLVMMetadataRef CreateSubroutineType(LLVMDIBuilderRef builder, LLVMMetadataRef debugMetadataFile,
//            LLVMValueRef llvmFunction)
//        {
////            byte[] methodNameBytes = Encoding.ASCII.GetBytes("debugType");
//            //            fixed (byte* pMethodName = methodNameBytes)
//            //            {
//            //                sbyte* methodNameSBytePtr = (sbyte*)pMethodName;
//            //
////            LLVMMetadataRef[] paramTypes = new LLVMMetadataRef[llvmFunction.ParamsCount];
//                LLVMMetadataRef[] paramTypes = new LLVMMetadataRef[0];
////                for (uint i = 0; i < llvmFunction.ParamsCount; i++)
////                {
////                    var llvmParam = llvmFunction.GetParam(i);
////                    var metaType = LLVM.DIBuilderCreateBasicType(builder, methodNameSBytePtr, (UIntPtr)methodNameBytes.Length, (ulong)llvmParam.TypeOf.SizeOf.Pointer, 0, LLVMDIFlags.LLVMDIFlagZero);
////                    paramTypes[i] = metaType;
////                }
//
//                fixed (LLVMMetadataRef* pParameterTypes = new ReadOnlySpan<LLVMMetadataRef>(paramTypes))
//                {
//                    return new LLVMMetadataRef((IntPtr)LLVM.DIBuilderCreateSubroutineType(builder, debugMetadataFile,
//                        (LLVMOpaqueMetadata**)pParameterTypes, (uint)paramTypes.Length, 0));
//                }
////            }
//        }
//    }
}
