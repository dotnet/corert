// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMDIBuilderRef
    {
        public LLVMDIBuilderRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public LLVMMetadataRef CreateCompileUnit(LLVMDWARFSourceLanguage SourceLanguage, LLVMMetadataRef FileMetadata, string Producer, int IsOptimized, string Flags, uint RuntimeVersion,
            string SplitName, LLVMDWARFEmissionKind DwarfEmissionKind, uint DWOld, int SplitDebugInlining, int DebugInfoForProfiling)
        {
            using var marshaledProducer= new MarshaledString(Producer);
            using var marshaledFlags = new MarshaledString(Flags);
            using var marshaledSplitNameFlags = new MarshaledString(SplitName);

            return LLVM.DIBuilderCreateCompileUnit(this, SourceLanguage, FileMetadata, marshaledProducer, (UIntPtr)marshaledProducer.Length, IsOptimized, marshaledFlags, (UIntPtr)marshaledFlags.Length,
                RuntimeVersion, marshaledSplitNameFlags, (UIntPtr)marshaledSplitNameFlags.Length, DwarfEmissionKind, DWOld, SplitDebugInlining, DebugInfoForProfiling);
        }

        public LLVMMetadataRef CreateFile(string FullPath, string Directory)
        {
            using var marshaledFullPath = new MarshaledString(FullPath);
            using var marshaledDirectory = new MarshaledString(Directory);
            return LLVM.DIBuilderCreateFile(this, marshaledFullPath, (UIntPtr)marshaledFullPath.Length, marshaledDirectory, (UIntPtr)marshaledDirectory.Length);
        }

        public LLVMMetadataRef CreateFunction(LLVMMetadataRef Scope, string Name, string LinkageName, LLVMMetadataRef File, uint LineNo, LLVMMetadataRef Type, int IsLocalToUnit, int IsDefinition,
            uint ScopeLine, LLVMDIFlags Flags, int IsOptimized)
        {
            using var marshaledName = new MarshaledString(Name);
            using var marshaledLinkageName = new MarshaledString(LinkageName);

            return LLVM.DIBuilderCreateFunction(this, Scope, marshaledName, (UIntPtr)marshaledName.Length, marshaledLinkageName, (UIntPtr)marshaledLinkageName.Length, File,
                LineNo, Type, IsLocalToUnit, IsDefinition, ScopeLine, Flags, IsOptimized);
        }

        public LLVMMetadataRef CreateSubroutineType(LLVMMetadataRef File, ReadOnlySpan<LLVMMetadataRef> ParameterTypes, LLVMDIFlags Flags)
        {
            fixed (LLVMMetadataRef* pParameterTypes = ParameterTypes)
            {
                return LLVM.DIBuilderCreateSubroutineType(this, File, (LLVMOpaqueMetadata**)pParameterTypes, (uint)ParameterTypes.Length, Flags);
            }
        }

        public void DIBuilderFinalize()
        {
            LLVM.DIBuilderFinalize(this);
        }


        public static implicit operator LLVMDIBuilderRef(LLVMOpaqueDIBuilder* value)
        {
            return new LLVMDIBuilderRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueDIBuilder*(LLVMDIBuilderRef value)
        {
            return (LLVMOpaqueDIBuilder*)value.Handle;
        }
    }
}
