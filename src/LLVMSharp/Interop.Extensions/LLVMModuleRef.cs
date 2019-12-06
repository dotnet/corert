// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMModuleRef : IDisposable, IEquatable<LLVMModuleRef>
    {
        public LLVMModuleRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMModuleRef(LLVMOpaqueModule* value)
        {
            return new LLVMModuleRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueModule*(LLVMModuleRef value)
        {
            return (LLVMOpaqueModule*)value.Handle;
        }

        public LLVMContextRef Context => (Handle != IntPtr.Zero) ? LLVM.GetModuleContext(this) : default;

        public string DataLayout
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pDataLayoutStr = LLVM.GetDataLayout(this);

                if (pDataLayoutStr is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pDataLayoutStr, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            set
            {
                using var marshaledDataLayoutStr = new MarshaledString(value);
                LLVM.SetDataLayout(this, marshaledDataLayoutStr);
            }
        }

        public LLVMValueRef FirstFunction => (Handle != IntPtr.Zero) ? LLVM.GetFirstFunction(this) : default;

        public LLVMValueRef FirstGlobal => (Handle != IntPtr.Zero) ? LLVM.GetFirstGlobal(this) : default;

        public LLVMValueRef LastFunction => (Handle != IntPtr.Zero) ? LLVM.GetLastFunction(this) : default;

        public LLVMValueRef LastGlobal => (Handle != IntPtr.Zero) ? LLVM.GetLastGlobal(this) : default;

        public string Target
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pTriple = LLVM.GetTarget(this);

                if (pTriple is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pTriple, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            set
            {
                using var marshaledTriple = new MarshaledString(value);
                LLVM.SetTarget(this, marshaledTriple);
            }
        }

        public static bool operator ==(LLVMModuleRef left, LLVMModuleRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMModuleRef left, LLVMModuleRef right) => !(left == right);

        public static LLVMModuleRef CreateWithName(string ModuleID)
        {
            using var marshaledModuleID = new MarshaledString(ModuleID);
            return LLVM.ModuleCreateWithName(marshaledModuleID);
        }

        public LLVMValueRef AddAlias(LLVMTypeRef Ty, LLVMValueRef Aliasee, string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.AddAlias(this, Ty, Aliasee, marshaledName);
        }

        public LLVMValueRef AddFunction(string Name, LLVMTypeRef FunctionTy)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.AddFunction(this, marshaledName, FunctionTy);
        }

        public LLVMValueRef AddGlobal(LLVMTypeRef Ty, string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.AddGlobal(this, Ty, marshaledName);
        }

        public LLVMValueRef AddGlobalInAddressSpace(LLVMTypeRef Ty, string Name, uint AddressSpace)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.AddGlobalInAddressSpace(this, Ty, marshaledName, AddressSpace);
        }

        public void AddNamedMetadataOperand(string Name, LLVMValueRef Val)
        {
            using var marshaledName = new MarshaledString(Name);
            LLVM.AddNamedMetadataOperand(this, marshaledName, Val);
        }

        public LLVMDIBuilderRef CreateDIBuilder()
        {
            return new LLVMDIBuilderRef((IntPtr)LLVM.CreateDIBuilder(this));
        }

        public LLVMExecutionEngineRef CreateExecutionEngine()
        {
            if (!TryCreateExecutionEngine(out LLVMExecutionEngineRef EE, out string Error))
            {
                throw new ExternalException(Error);
            }

            return EE;
        }

        public LLVMExecutionEngineRef CreateInterpreter()
        {
            if (!TryCreateInterpreter(out LLVMExecutionEngineRef Interp, out string Error))
            {
                throw new ExternalException(Error);
            }

            return Interp;
        }

        public LLVMExecutionEngineRef CreateMCJITCompiler()
        {
            if (!TryCreateMCJITCompiler(out LLVMExecutionEngineRef JIT, out string Error))
            {
                throw new ExternalException(Error);
            }

            return JIT;
        }

        public LLVMExecutionEngineRef CreateMCJITCompiler(ref LLVMMCJITCompilerOptions Options)
        {
            if (!TryCreateMCJITCompiler(out LLVMExecutionEngineRef JIT, ref Options, out string Error))
            {
                throw new ExternalException(Error);
            }

            return JIT;
        }

        public LLVMModuleRef Clone() => LLVM.CloneModule(this);

        public LLVMPassManagerRef CreateFunctionPassManager() => LLVM.CreateFunctionPassManagerForModule(this);

        public LLVMModuleProviderRef CreateModuleProvider() => LLVM.CreateModuleProviderForExistingModule(this);

        public void AddNamedMetadataOperand(string Name, LLVMMetadataRef CompileUnitMetadata)
        {
            using var marshaledName = new MarshaledString(Name);
            LLVM.AddNamedMetadataOperand(this, marshaledName, LLVM.MetadataAsValue(Context, CompileUnitMetadata));
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                LLVM.DisposeModule(this);
                Handle = IntPtr.Zero;
            }
        }

        public void Dump() => LLVM.DumpModule(this);

        public override bool Equals(object obj) => obj is LLVMModuleRef other && Equals(other);

        public bool Equals(LLVMModuleRef other) => Handle == other.Handle;

        public LLVMValueRef GetNamedFunction(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.GetNamedFunction(this, marshaledName);
        }

        public override int GetHashCode() => Handle.GetHashCode();

        public LLVMValueRef GetNamedGlobal(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.GetNamedGlobal(this, marshaledName);
        }

        public LLVMValueRef[] GetNamedMetadataOperands(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            var Dest = new LLVMValueRef[LLVM.GetNamedMetadataNumOperands(this, marshaledName)];

            fixed (LLVMValueRef* pDest = Dest)
            {
                LLVM.GetNamedMetadataOperands(this, marshaledName, (LLVMOpaqueValue**)pDest);
            }

            return Dest;
        }

        public uint GetNamedMetadataOperandsCount(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.GetNamedMetadataNumOperands(this, marshaledName);
        }

        public LLVMTypeRef GetTypeByName(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.GetTypeByName(this, marshaledName);
        }

        public void PrintToFile(string Filename)
        {
            if (!TryPrintToFile(Filename, out string ErrorMessage))
            {
                throw new ExternalException(ErrorMessage);
            }
        }

        public string PrintToString()
        {
            var pStr = LLVM.PrintModuleToString(this);

            if (pStr is null)
            {
                return string.Empty;
            }
            var span = new ReadOnlySpan<byte>(pStr, int.MaxValue);

            var result = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            LLVM.DisposeMessage(pStr);
            return result;
        }

        public void SetModuleInlineAsm(string Asm)
        {
            using var marshaledAsm = new MarshaledString(Asm);
            LLVM.SetModuleInlineAsm(this, marshaledAsm);
        }

        public override string ToString() => (Handle != IntPtr.Zero) ? PrintToString() : string.Empty;

        public bool TryCreateExecutionEngine(out LLVMExecutionEngineRef OutEE, out string OutError)
        {
            fixed (LLVMExecutionEngineRef* pOutEE = &OutEE)
            {
                sbyte* pError;
                var result = LLVM.CreateExecutionEngineForModule((LLVMOpaqueExecutionEngine**)pOutEE, this, &pError);

                if (pError is null)
                {
                    OutError = string.Empty;
                }
                else
                {
                    var span = new ReadOnlySpan<byte>(pError, int.MaxValue);
                    OutError = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
                }

                return result == 0;
            }
        }

        public bool TryCreateInterpreter(out LLVMExecutionEngineRef OutInterp, out string OutError)
        {
            fixed (LLVMExecutionEngineRef* pOutInterp = &OutInterp)
            {
                sbyte* pError;
                var result = LLVM.CreateInterpreterForModule((LLVMOpaqueExecutionEngine**)pOutInterp, this, &pError);

                if (pError is null)
                {
                    OutError = string.Empty;
                }
                else
                {
                    var span = new ReadOnlySpan<byte>(pError, int.MaxValue);
                    OutError = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
                }

                return result == 0;
            }
        }

        public bool TryCreateMCJITCompiler(out LLVMExecutionEngineRef OutJIT, out string OutError)
        {
            var Options = LLVMMCJITCompilerOptions.Create();
            return TryCreateMCJITCompiler(out OutJIT, ref Options, out OutError);
        }

        public bool TryCreateMCJITCompiler(out LLVMExecutionEngineRef OutJIT, ref LLVMMCJITCompilerOptions Options, out string OutError)
        {
            fixed (LLVMExecutionEngineRef* pOutJIT = &OutJIT)
            fixed (LLVMMCJITCompilerOptions* pOptions = &Options)
            {
                sbyte* pError;
                var result = LLVM.CreateMCJITCompilerForModule((LLVMOpaqueExecutionEngine**)pOutJIT, this, pOptions, (UIntPtr)Marshal.SizeOf<LLVMMCJITCompilerOptions>(), &pError);

                if (pError is null)
                {
                    OutError = string.Empty;
                }
                else
                {
                    var span = new ReadOnlySpan<byte>(pError, int.MaxValue);
                    OutError = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
                }

                return result == 0;
            }
        }

        public bool TryPrintToFile(string Filename, out string ErrorMessage)
        {
            using var marshaledFilename = new MarshaledString(Filename);

            sbyte* pErrorMessage = null;
            int result = 0;
            try
            {
                result = LLVM.PrintModuleToFile(this, marshaledFilename, &pErrorMessage);
            }
            catch (Exception)
            {

            }

            if (pErrorMessage is null)
            {
                ErrorMessage = string.Empty;
            }
            else
            {
                var span = new ReadOnlySpan<byte>(pErrorMessage, int.MaxValue);
                ErrorMessage = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            return result == 0;
        }

        public bool TryVerify(LLVMVerifierFailureAction Action, out string OutMessage)
        {
            sbyte* pMessage;
            var result = LLVM.VerifyModule(this, Action, &pMessage);

            if (pMessage is null)
            {
                OutMessage = string.Empty;
            }
            else
            {
                var span = new ReadOnlySpan<byte>(pMessage, int.MaxValue);
                OutMessage = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            return result == 0;
        }

        public void Verify(LLVMVerifierFailureAction Action)
        {
            if (!TryVerify(Action, out string Message))
            {
                throw new ExternalException(Message);
            }
        }

        public int WriteBitcodeToFile(string Path)
        {
            using var marshaledPath = new MarshaledString(Path);
            return LLVM.WriteBitcodeToFile(this, marshaledPath);
        }

        public int WriteBitcodeToFD(int FD, int ShouldClose, int Unbuffered) => LLVM.WriteBitcodeToFD(this, FD, ShouldClose, Unbuffered);

        public int WriteBitcodeToFileHandle(int Handle) => LLVM.WriteBitcodeToFileHandle(this, Handle);

        public LLVMMemoryBufferRef WriteBitcodeToMemoryBuffer() => LLVM.WriteBitcodeToMemoryBuffer(this);
    }
}
