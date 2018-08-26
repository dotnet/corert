// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using LLVMSharp;

namespace ILCompiler.WebAssembly
{
    // LLVM P/Invokes copied from LLVMSharp that match the current LLVM surface area.
    // If we get a new version of LLVMSharp containing these, this file should be removed.
    internal class LLVMPInvokes
    {
        const string libraryPath = "libLLVM";
        [DllImport(libraryPath, CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMDIBuilderRef LLVMCreateDIBuilder(LLVMModuleRef M);

        [DllImport(libraryPath, EntryPoint = "LLVMDIBuilderCreateCompileUnit", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMetadataRef LLVMDIBuilderCreateCompileUnit(LLVMDIBuilderRef @Builder, LLVMDWARFSourceLanguage @Lang, LLVMMetadataRef @FileRef, [MarshalAs(UnmanagedType.LPStr)] string @Producer, size_t @ProducerLen, LLVMBool @isOptimized, [MarshalAs(UnmanagedType.LPStr)] string @Flags, size_t @FlagsLen, uint @RuntimeVer, [MarshalAs(UnmanagedType.LPStr)] string @SplitName, size_t @SplitNameLen, LLVMDWARFEmissionKind @Kind, uint @DWOId, LLVMBool @SplitDebugInlining, LLVMBool @DebugInfoForProfiling);

        [DllImport(libraryPath, EntryPoint = "LLVMDIBuilderCreateFile", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMetadataRef LLVMDIBuilderCreateFile(LLVMDIBuilderRef @Builder, [MarshalAs(UnmanagedType.LPStr)] string @Filename, size_t @FilenameLen, [MarshalAs(UnmanagedType.LPStr)] string @Directory, size_t @DirectoryLen);

        [DllImport(libraryPath, EntryPoint = "LLVMDIBuilderCreateDebugLocation", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMMetadataRef LLVMDIBuilderCreateDebugLocation(LLVMContextRef @Ctx, uint @Line, uint @Column, LLVMMetadataRef @Scope, LLVMMetadataRef @InlinedAt);
    }

    internal enum LLVMDWARFSourceLanguage : int
    {
        @LLVMDWARFSourceLanguageC89 = 0,
        @LLVMDWARFSourceLanguageC = 1,
        @LLVMDWARFSourceLanguageAda83 = 2,
        @LLVMDWARFSourceLanguageC_plus_plus = 3,
        @LLVMDWARFSourceLanguageCobol74 = 4,
        @LLVMDWARFSourceLanguageCobol85 = 5,
        @LLVMDWARFSourceLanguageFortran77 = 6,
        @LLVMDWARFSourceLanguageFortran90 = 7,
        @LLVMDWARFSourceLanguagePascal83 = 8,
        @LLVMDWARFSourceLanguageModula2 = 9,
        @LLVMDWARFSourceLanguageJava = 10,
        @LLVMDWARFSourceLanguageC99 = 11,
        @LLVMDWARFSourceLanguageAda95 = 12,
        @LLVMDWARFSourceLanguageFortran95 = 13,
        @LLVMDWARFSourceLanguagePLI = 14,
        @LLVMDWARFSourceLanguageObjC = 15,
        @LLVMDWARFSourceLanguageObjC_plus_plus = 16,
        @LLVMDWARFSourceLanguageUPC = 17,
        @LLVMDWARFSourceLanguageD = 18,
        @LLVMDWARFSourceLanguagePython = 19,
        @LLVMDWARFSourceLanguageOpenCL = 20,
        @LLVMDWARFSourceLanguageGo = 21,
        @LLVMDWARFSourceLanguageModula3 = 22,
        @LLVMDWARFSourceLanguageHaskell = 23,
        @LLVMDWARFSourceLanguageC_plus_plus_03 = 24,
        @LLVMDWARFSourceLanguageC_plus_plus_11 = 25,
        @LLVMDWARFSourceLanguageOCaml = 26,
        @LLVMDWARFSourceLanguageRust = 27,
        @LLVMDWARFSourceLanguageC11 = 28,
        @LLVMDWARFSourceLanguageSwift = 29,
        @LLVMDWARFSourceLanguageJulia = 30,
        @LLVMDWARFSourceLanguageDylan = 31,
        @LLVMDWARFSourceLanguageC_plus_plus_14 = 32,
        @LLVMDWARFSourceLanguageFortran03 = 33,
        @LLVMDWARFSourceLanguageFortran08 = 34,
        @LLVMDWARFSourceLanguageRenderScript = 35,
        @LLVMDWARFSourceLanguageBLISS = 36,
        @LLVMDWARFSourceLanguageMips_Assembler = 37,
        @LLVMDWARFSourceLanguageGOOGLE_RenderScript = 38,
        @LLVMDWARFSourceLanguageBORLAND_Delphi = 39,
    }

    internal enum LLVMDWARFEmissionKind : int
    {
        @LLVMDWARFEmissionNone = 0,
        @LLVMDWARFEmissionFull = 1,
        @LLVMDWARFEmissionLineTablesOnly = 2,
    }
}
