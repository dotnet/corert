// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILVerify;

namespace Internal.IL
{
    class VerificationException : Exception
    {
        public VerificationException()
        {
        }
    }

    class LocalVerificationException : VerificationException
    {
        public LocalVerificationException()
        {
        }
    }

    struct VerificationErrorArgs
    {
        public VerifierError Code;
        public int Offset;
        public int Token;
        public string Found;
        public string Expected;
    }

    partial class ILImporter
    {
        readonly MethodDesc _method;
        readonly MethodSignature _methodSignature;
        readonly TypeSystemContext _typeSystemContext;

        readonly TypeDesc _thisType;

        readonly MethodIL _methodIL;
        readonly byte[] _ilBytes;
        readonly LocalVariableDefinition[] _locals;

        readonly bool _initLocals;
        readonly int _maxStack;

        bool[] _instructionBoundaries; // For IL verification

        static readonly StackValue[] s_emptyStack = new StackValue[0];
        StackValue[] _stack = s_emptyStack;
        int _stackTop = 0;

        class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        };
        ExceptionRegion[] _exceptionRegions;

        [Flags]
        enum Prefix
        {
            ReadOnly    = 0x01,
            Unaligned   = 0x02,
            Volatile    = 0x04,
            Tail        = 0x08,
            Constrained = 0x10,
            No          = 0x20,
        }
        Prefix _pendingPrefix;
        TypeDesc _constrained;

        int _currentInstructionOffset;

        class BasicBlock
        {
            // Common fields
            public BasicBlock Next;

            public int StartOffset;
            public int EndOffset;

            public StackValue[] EntryStack;

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;

            //these point to the direct enclosing items in _exceptionRegions
            public int? TryIndex;
            public int? HandlerIndex;
            public int? FilterIndex;
        };

        void EmptyTheStack() => _stackTop = 0;

        void Push(StackValue value)
        {
            FatalCheck(_stackTop < _maxStack, VerifierError.StackOverflow);

            if (_stackTop >= _stack.Length)
                Array.Resize(ref _stack, 2 * _stackTop + 3);
            _stack[_stackTop++] = value;
        }

        StackValue Pop()
        {
            FatalCheck(_stackTop > 0, VerifierError.StackUnderflow);

            return _stack[--_stackTop];
        }

        public ILImporter(MethodDesc method, MethodIL methodIL)
        {
            _method = method;
            _methodSignature = method.Signature;
            _typeSystemContext = method.Context;

            if (!_methodSignature.IsStatic)
            {
                var thisType = method.OwningType.InstantiateAsOpen();

                // ECMA-335 II.13.3 Methods of value types, P. 164:
                // ... By contrast, instance and virtual methods of value types shall be coded to expect a
                // managed pointer(see Partition I) to an unboxed instance of the value type. ...
                if (thisType.IsValueType)
                    thisType = thisType.MakeByRefType();

                _thisType = thisType;
            }

            _methodIL = methodIL;

            _initLocals = _methodIL.IsInitLocals;

            _maxStack = _methodIL.MaxStack;

            _ilBytes = _methodIL.GetILBytes();
            _locals = _methodIL.GetLocals();

            var ilExceptionRegions = _methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
        }

        public Action<VerificationErrorArgs> ReportVerificationError
        {
            set;
            private get;
        }

        public void Verify()
        {
            _instructionBoundaries = new bool[_ilBytes.Length];

            FindBasicBlocks();
            FindEnclosingExceptionRegions();
            ImportBasicBlocks();
        }

        private void FindEnclosingExceptionRegions()
        {
            for (int i = 0; i < _basicBlocks.Length; i++)
            {
                if (_basicBlocks[i] == null)
                    continue;

                var basicBlock = _basicBlocks[i];
                var offset = basicBlock.StartOffset;

                for (int j = 0; j < _exceptionRegions.Length; j++)
                {
                    var r = _exceptionRegions[j].ILRegion;
                    // Check if offset is within the range [TryOffset, TryOffset + TryLength[
                    if (r.TryOffset <= offset && offset < r.TryOffset + r.TryLength)
                    {
                        if (!basicBlock.TryIndex.HasValue)
                        {
                            basicBlock.TryIndex = j;
                        }
                        else
                        {
                            var currentlySelected = _exceptionRegions[basicBlock.TryIndex.Value].ILRegion;
                            var probeItem = _exceptionRegions[j].ILRegion;

                            if (currentlySelected.TryOffset < probeItem.TryOffset &&
                                currentlySelected.TryOffset + currentlySelected.TryLength > probeItem.TryOffset + probeItem.TryLength)
                            {
                                basicBlock.TryIndex = j;
                            }
                        }
                    }
                    // Check if offset is within the range [HandlerOffset, HandlerOffset + HandlerLength[
                    if (r.HandlerOffset <= offset && offset < r.HandlerOffset + r.HandlerLength)
                    {
                        if (!basicBlock.HandlerIndex.HasValue)
                        {
                            basicBlock.HandlerIndex = j;
                        }
                        else
                        {
                            var currentlySelected = _exceptionRegions[basicBlock.HandlerIndex.Value].ILRegion;
                            var probeItem = _exceptionRegions[j].ILRegion;

                            if (currentlySelected.HandlerOffset < probeItem.HandlerOffset &&
                                currentlySelected.HandlerOffset + currentlySelected.HandlerLength > probeItem.HandlerOffset + probeItem.HandlerLength)
                            {
                                basicBlock.HandlerIndex = j;
                            }
                        }
                    }
                    // Check if offset is within the range [FilterOffset, HandlerOffset[
                    if (r.FilterOffset != -1 && r.FilterOffset <= offset && offset < r.HandlerOffset )
                    {
                        if(!basicBlock.FilterIndex.HasValue)
                        {
                            basicBlock.FilterIndex = j;
                        }
                    }
                }
            }
        }

        void AbortBasicBlockVerification()
        {
            throw new LocalVerificationException();
        }

        void AbortMethodVerification()
        {
            throw new VerificationException();
        }

        // Check whether the condition is true. If not, terminate the verification of current method.
        void FatalCheck(bool cond, VerifierError error)
        {
            if (!Check(cond, error))
                AbortMethodVerification();
        }

        // Check whether the condition is true. If not, terminate the verification of current method.
        void FatalCheck(bool cond, VerifierError error, StackValue found)
        {
            if (!Check(cond, error, found))
                AbortMethodVerification();
        }

        // Check whether the condition is true. If not, terminate the verification of current method.
        void FatalCheck(bool cond, VerifierError error, StackValue found, StackValue expected)
        {
            if (!Check(cond, error, found, expected))
                AbortMethodVerification();
        }

        // If not, report verification error and continue verification.
        void VerificationError(VerifierError error)
        {
            var args = new VerificationErrorArgs() { Code = error, Offset = _currentInstructionOffset };
            ReportVerificationError(args);
        }

        void VerificationError(VerifierError error, object found)
        {
            var args = new VerificationErrorArgs()
            {
                Code = error,
                Offset = _currentInstructionOffset,
                Found = found.ToString(),
            };
            ReportVerificationError(args);
        }

        void VerificationError(VerifierError error, object found, object expected)
        {
            var args = new VerificationErrorArgs()
            {
                Code = error, 
                Offset = _currentInstructionOffset,
                Found = found.ToString(),
                Expected = expected.ToString(),
            };
            ReportVerificationError(args);
        }

        // Check whether the condition is true. If not, report verification error and continue verification.
        bool Check(bool cond, VerifierError error)
        {
            if (!cond)
                VerificationError(error);
            return cond;
        }

        bool Check(bool cond, VerifierError error, StackValue found)
        {
            if (!cond)
                VerificationError(error, found);
            return cond;
        }

        bool Check(bool cond, VerifierError error, StackValue found, StackValue expected)
        {
            if (!cond)
                VerificationError(error, found, expected);
            return cond;
        }

        void CheckIsNumeric(StackValue value)
        {
            if (!Check(StackValueKind.Int32 <= value.Kind && value.Kind <= StackValueKind.Float, 
                VerifierError.ExpectedNumericType, value))
            {
                AbortBasicBlockVerification();
            }
        }

        void CheckIsInteger(StackValue value)
        {
            if (!Check(StackValueKind.Int32 <= value.Kind && value.Kind <= StackValueKind.NativeInt,
                VerifierError.ExpectedIntegerType, value))
            {
                AbortBasicBlockVerification();
            }
        }

        void CheckIsIndex(StackValue value)
        {
            if (!Check(value.Kind == StackValueKind.Int32 || value.Kind == StackValueKind.NativeInt,
                VerifierError.StackUnexpected /* TODO: ExpectedIndex */, value))
            {
                AbortBasicBlockVerification();
            }
        }

        void CheckIsByRef(StackValue value)
        {
            if (!Check(value.Kind == StackValueKind.ByRef, VerifierError.StackByRef, value))
            {
                AbortBasicBlockVerification();
            }
        }

        void CheckIsArray(StackValue value)
        {
            Check((value.Kind == StackValueKind.ObjRef) && ((value.Type == null) || value.Type.IsSzArray), 
                VerifierError.ExpectedArray /* , value */);
        }

        void CheckIsAssignable(StackValue src, StackValue dst)
        {
            if (!IsAssignable(src, dst))
                VerificationError(VerifierError.StackUnexpected, src, dst);
        }

        // For now, match PEVerify type formating to make it easy to compare with baseline
        static string TypeToStringForIsAssignable(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean: return "Boolean";
                case TypeFlags.Char: return "Char";
                case TypeFlags.SByte:
                case TypeFlags.Byte: return "Byte";
                case TypeFlags.Int16: 
                case TypeFlags.UInt16: return "Short";
                case TypeFlags.Int32:
                case TypeFlags.UInt32: return "Int32";
                case TypeFlags.Int64: 
                case TypeFlags.UInt64: return "Long";
                case TypeFlags.Single: return "Single";
                case TypeFlags.Double: return "Double";
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr: return "Native Int";
            }

            return StackValue.CreateFromType(type).ToString();
        }

        void CheckIsAssignable(TypeDesc src, TypeDesc dst)
        {
            if (!IsAssignable(src, dst))
            {
                VerificationError(VerifierError.StackUnexpected, TypeToStringForIsAssignable(src), TypeToStringForIsAssignable(dst));
            }
        }

        void CheckIsArrayElementCompatibleWith(TypeDesc src, TypeDesc dst)
        {
            if (!IsAssignable(src, dst, true))
            {
                // TODO: Better error message
                VerificationError(VerifierError.StackUnexpected, TypeToStringForIsAssignable(src));
            }
        }

        void CheckIsPointerElementCompatibleWith(TypeDesc src, TypeDesc dst)
        {
            if (!(src == dst || IsSameReducedType(src, dst)))
            {
                // TODO: Better error message
                // VerificationError(VerifierError.StackUnexpected, TypeToStringForIsAssignable(src), TypeToStringForIsAssignable(dst));
                VerificationError(VerifierError.StackUnexpectedArrayType, TypeToStringForIsAssignable(src));
            }
        }

        void CheckIsObjRef(TypeDesc type)
        {
            if (!IsAssignable(type, GetWellKnownType(WellKnownType.Object), false))
            {
                // TODO: Better error message
                VerificationError(VerifierError.StackUnexpected, TypeToStringForIsAssignable(type));
            }
        }

        void CheckIsComparable(StackValue a, StackValue b, ILOpcode op)
        {
            if (!IsBinaryComparable(a, b, op))
            {
                VerificationError(VerifierError.StackUnexpected, a, b);
            }
        }

        void Unverifiable()
        {
            VerificationError(VerifierError.Unverifiable);
        }

        TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _typeSystemContext.GetWellKnownType(wellKnownType);
        }

        void HandleTokenResolveException(int token)
        {
            var args = new VerificationErrorArgs()
            {
                Code = VerifierError.TokenResolve,
                Offset = _currentInstructionOffset,
                Token = token
            };
            ReportVerificationError(args);
            AbortBasicBlockVerification();
        }

        Object ResolveToken(int token)
        {
            Object tokenObj = null;
            try
            {
                tokenObj = _methodIL.GetObject(token);
            }
            catch (BadImageFormatException)
            {
                HandleTokenResolveException(token);
            }
            catch (ArgumentException)
            {
                HandleTokenResolveException(token);
            }
            return tokenObj;
        }

        TypeDesc ResolveTypeToken(int token)
        {
            object tokenObj = ResolveToken(token);
            FatalCheck(tokenObj is TypeDesc, VerifierError.ExpectedTypeToken);
            return (TypeDesc)tokenObj;
        }

        FieldDesc ResolveFieldToken(int token)
        {
            object tokenObj = ResolveToken(token);
            FatalCheck(tokenObj is FieldDesc, VerifierError.ExpectedFieldToken);
            return (FieldDesc)tokenObj;
        }

        MethodDesc ResolveMethodToken(int token)
        {
            object tokenObj = ResolveToken(token);
            FatalCheck(tokenObj is MethodDesc, VerifierError.ExpectedMethodToken);
            return (MethodDesc)tokenObj;
        }

        void MarkInstructionBoundary()
        {
            _instructionBoundaries[_currentOffset] = true;
        }

        void StartImportingInstruction()
        {
            _currentInstructionOffset = _currentOffset;
        }

        void EndImportingInstruction()
        {
            CheckPendingPrefix(_pendingPrefix);
        }

        void StartImportingBasicBlock(BasicBlock basicBlock)
        {
            if (basicBlock.TryStart)
            {
                Check(basicBlock.EntryStack == null || basicBlock.EntryStack.Length == 0, VerifierError.TryNonEmptyStack);

                for (int i = 0; i < _exceptionRegions.Length; i++)
                {
                    var r = _exceptionRegions[i];

                    if (basicBlock.StartOffset != r.ILRegion.TryOffset)
                        continue;

                    if (r.ILRegion.Kind == ILExceptionRegionKind.Filter)
                        MarkBasicBlock(_basicBlocks[r.ILRegion.FilterOffset]);
                    
                    MarkBasicBlock(_basicBlocks[r.ILRegion.HandlerOffset]);
                }
            }

            if (basicBlock.FilterStart || basicBlock.HandlerStart)
            {
                Debug.Assert(basicBlock.EntryStack == null);

                ExceptionRegion r;
                if (basicBlock.HandlerIndex.HasValue)
                {
                    r = _exceptionRegions[basicBlock.HandlerIndex.Value];
                }
                else if (basicBlock.FilterIndex.HasValue)
                {
                    r = _exceptionRegions[basicBlock.FilterIndex.Value];
                }
                else
                {
                    return;
                }

                if (r.ILRegion.Kind == ILExceptionRegionKind.Filter)
                {
                    basicBlock.EntryStack = new StackValue[] { StackValue.CreateObjRef(GetWellKnownType(WellKnownType.Object)) };
                }
                else
                if (r.ILRegion.Kind == ILExceptionRegionKind.Catch)
                {
                    basicBlock.EntryStack = new StackValue[] { StackValue.CreateObjRef(ResolveTypeToken(r.ILRegion.ClassToken)) };
                }
                else
                {
                    basicBlock.EntryStack = s_emptyStack;
                }
            }

            if (basicBlock.EntryStack?.Length > 0)
            {
                if (_stack == null || _stack.Length < basicBlock.EntryStack.Length)
                    Array.Resize(ref _stack, basicBlock.EntryStack.Length);
                Array.Copy(basicBlock.EntryStack, _stack, basicBlock.EntryStack.Length);
                _stackTop = basicBlock.EntryStack.Length;
            }
            else
            {
                _stackTop = 0;
            }
        }

        void EndImportingBasicBlock(BasicBlock basicBlock)
        {
        }

        void ImportNop()
        {
            // Always verifiable
        }

        void ImportBreak()
        {
            // Always verifiable
        }

        TypeDesc GetVarType(int index, bool argument)
        {
            if (argument)
            {
                if (_thisType != null)
                {
                    if (index == 0)
                        return _thisType;
                    index--;
                }
                FatalCheck(index < _methodSignature.Length, VerifierError.UnrecognizedArgumentNumber);
                return _methodSignature[index];
            }
            else
            {
                FatalCheck(index < _locals.Length, VerifierError.UnrecognizedLocalNumber);
                return _locals[index].Type;
            }
        }

        void ImportLoadVar(int index, bool argument)
        {
            var varType = GetVarType(index, argument);

            if (!argument)
                Check(_initLocals, VerifierError.InitLocals);

            Push(StackValue.CreateFromType(varType));
        }

        void ImportStoreVar(int index, bool argument)
        {
            var varType = GetVarType(index, argument);

            var value = Pop();

            CheckIsAssignable(value, StackValue.CreateFromType(varType));
        }

        void ImportAddressOfVar(int index, bool argument)
        {
            var varType = GetVarType(index, argument);

            if (!argument)
                Check(_initLocals, VerifierError.InitLocals);

            Push(StackValue.CreateByRef(varType));
        }

        void ImportDup()
        {
            var value = Pop();

            Push(value);
            Push(value);
        }

        void ImportPop()
        {
            Pop();
        }

        void ImportJmp(int token)
        {
            Unverifiable();

            var method = ResolveMethodToken(token);
        }

        void ImportCasting(ILOpcode opcode, int token)
        {
            var type = ResolveTypeToken(token);

            var value = Pop();

            // TODO
            // VerifyIsObjRef(tiVal);

            // TODO
            // verCheckClassAccess(pResolvedToken);

            Push(StackValue.CreateObjRef(type));
        }

        void ImportCall(ILOpcode opcode, int token)
        {
            FatalCheck(opcode != ILOpcode.calli, VerifierError.Unverifiable);

            TypeDesc constrained = null;
            bool tailCall = false;

            if (opcode != ILOpcode.newobj)
            {
                if (HasPendingPrefix(Prefix.Constrained) && opcode == ILOpcode.callvirt)
                {
                    ClearPendingPrefix(Prefix.Constrained);
                    constrained = _constrained;
                }

                if (HasPendingPrefix(Prefix.Tail))
                {
                    ClearPendingPrefix(Prefix.Tail);
                    tailCall = true;
                }
            }

            // TODO: VarArgs
            // if (sig.isVarArg())
            //      eeGetCallSiteSig(memberRef, getCurrentModuleHandle(), getCurrentContext(), &sig, false);

            MethodDesc method = ResolveMethodToken(token);

            MethodSignature sig = method.Signature;

            TypeDesc methodType = sig.IsStatic ? null : method.OwningType;

            if (opcode == ILOpcode.callvirt)
            {
                Check(methodType != null, VerifierError.CallVirtOnStatic);
                Check(!methodType.IsValueType, VerifierError.CallVirtOnValueType);
            }
            else
            {
                EcmaMethod ecmaMethod = method.GetTypicalMethodDefinition() as EcmaMethod;
                if (ecmaMethod != null)
                    Check(!ecmaMethod.IsAbstract, VerifierError.CallAbstract);
            }

            for (int i = sig.Length - 1; i >= 0; i--)
            {
                var actual = Pop();
                var declared = StackValue.CreateFromType(sig[i]);

                CheckIsAssignable(actual, declared);

                // check that the argument is not a byref for tailcalls
                if (tailCall)
                    Check(!IsByRefLike(declared), VerifierError.TailByRef, declared);
            }

            if (opcode == ILOpcode.newobj)
            {
                // TODO:
            }
            else
            if (methodType != null)
            {
                var actualThis = Pop();
                var declaredThis = methodType.IsValueType ?
                    StackValue.CreateByRef(methodType) : StackValue.CreateObjRef(methodType);

#if false
                // If this is a call to the base class .ctor, set thisPtr Init for
                // this block.
                if (mflags & CORINFO_FLG_CONSTRUCTOR)
                {
                    if (m_verTrackObjCtorInitState && tiThis.IsThisPtr()
                        && verIsCallToInitThisPtr(getCurrentMethodClass(), methodClassHnd))
                    {
                        // do not allow double init
                        Verify(vstate->thisInitialized == THISUNINIT
                               || vstate->thisInitialized == THISEHREACHED, MVER_E_PATH_THIS);

                        vstate->containsCtorCall = 1;
                        vstate->setThisInitialized();
                        vstate->thisInitializedThisBlock = true;
                        tiThis.SetInitialisedObjRef();
                    }
                    else
                    {
                        // We allow direct calls to value type constructors
                        // NB: we have to check that the contents of tiThis is a value type, otherwise we could use a constrained
                        // callvirt to illegally re-enter a .ctor on a value of reference type.
                        VerifyAndReportFound(tiThis.IsByRef() && DereferenceByRef(tiThis).IsValueClass(), tiThis, MVER_E_CALL_CTOR);
                    }
                }
#endif

                if (constrained != null)
                {
                    Check(actualThis.Kind == StackValueKind.ByRef, VerifierError.ConstrainedCallWithNonByRefThis);

                    // We just dereference this and test for equality
                    //todo: improve error - "this type mismatch with constrained type operand"
                    Check(actualThis.Type == constrained, VerifierError.StackUnexpected);

                    // Now pretend the this type is the boxed constrained type, for the sake of subsequent checks
                    actualThis = StackValue.CreateObjRef(constrained);
                }

#if false
                // To support direct calls on readonly byrefs, just pretend tiDeclaredThis is readonly too
                if(tiDeclaredThis.IsByRef() && tiThis.IsReadonlyByRef())
                {
                    tiDeclaredThis.SetIsReadonlyByRef();
                }
#endif

                CheckIsAssignable(actualThis, declaredThis);

#if false
                // Rules for non-virtual call to a non-final virtual method:
        
                // Define: 
                // The "this" pointer is considered to be "possibly written" if
                //   1. Its address have been taken (LDARGA 0) anywhere in the method.
                //   (or)
                //   2. It has been stored to (STARG.0) anywhere in the method.

                // A non-virtual call to a non-final virtual method is only allowed if
                //   1. The this pointer passed to the callee is an instance of a boxed value type. 
                //   (or)
                //   2. The this pointer passed to the callee is the current method's this pointer.
                //      (and) The current method's this pointer is not "possibly written".

                // Thus the rule is that if you assign to this ANYWHERE you can't make "base" calls to 
                // virtual methods.  (Luckily this does affect .ctors, since they are not virtual).    
                // This is stronger that is strictly needed, but implementing a laxer rule is significantly 
                // hard and more error prone.

                if (opcode == ReaderBaseNS::CEE_CALL 
                    && (mflags & CORINFO_FLG_VIRTUAL) 
                    && ((mflags & CORINFO_FLG_FINAL) == 0)
                    && (!verIsBoxedValueType(tiThis)))
                {
                    // always enforce for peverify
                    Verify(tiThis.IsThisPtr() && !thisPossiblyModified,
                           MVER_E_THIS_MISMATCH);
                }
#endif

                if (tailCall)
                {
                    // also check the specil tailcall rule
                    Check(!IsByRefLike(declaredThis), VerifierError.TailByRef, declaredThis);

                    // Tail calls on constrained calls should be illegal too:
                    // when instantiated at a value type, a constrained call may pass the address of a stack allocated value 
                    Check(constrained == null, VerifierError.TailByRef);
                }
            }

#if false
            // check any constraints on the callee's class and type parameters
            Verify(m_jitInfo->satisfiesClassConstraints(methodClassHnd),
                            MVER_E_UNSATISFIED_METHOD_PARENT_INST); //"method has unsatisfied class constraints"
            Verify(m_jitInfo->satisfiesMethodConstraints(methodClassHnd,methodHnd),
                            MVER_E_UNSATISFIED_METHOD_INST); //"method has unsatisfied method constraints"
    

            handleMemberAccessForVerification(callInfo.accessAllowed, callInfo.callsiteCalloutHelper,
                                                MVER_E_METHOD_ACCESS);

            if (mflags & CORINFO_FLG_PROTECTED)
            {
                Verify(m_jitInfo->canAccessFamily(getCurrentMethodHandle(), instanceClassHnd), MVER_E_METHOD_ACCESS);
            }
#endif

            TypeDesc returnType = sig.ReturnType;

            // special checks for tailcalls
            if (tailCall)
            {
                TypeDesc callerReturnType = _methodSignature.ReturnType;

                if (returnType.IsVoid || callerReturnType.IsVoid)
                {
                    Check(returnType.IsVoid && callerReturnType.IsVoid, VerifierError.TailRetVoid);
                }
                // else
                // if (returnType.IsValueType || callerReturnType.IsValueType)
                // {
                //      TODO: Check exact match
                // }
                else
                {
                    CheckIsAssignable(StackValue.CreateFromType(returnType), StackValue.CreateFromType(callerReturnType));
                }

                // for tailcall, stack must be empty
                Check(_stackTop == 0, VerifierError.TailStackEmpty);
            }

            // now push on the result
            if (opcode == ILOpcode.newobj)
            {
                Push(StackValue.CreateFromType(methodType));
            }
            else
            if (!returnType.IsVoid)
            {
                var returnValue = StackValue.CreateFromType(returnType);

#if false
                // "readonly." prefixed calls only allowed for the Address operation on arrays.
                // The methods supported by array types are under the control of the EE
                // so we can trust that only the Address operation returns a byref.
                if (readonlyCall)
                {
                    VerifyOrReturn ((methodClassFlgs & CORINFO_FLG_ARRAY) && tiRetVal.IsByRef(), 
                                    MVER_E_READONLY_UNEXPECTED_CALLEE);//"unexpected use of readonly prefix"
                    vstate->readonlyPrefix = false;
                    tiRetVal.SetIsReadonlyByRef();
                }

                if (tiRetVal.IsByRef())
                {                
                    tiRetVal.SetIsPermanentHomeByRef();
                }
#endif

                Push(returnValue);
            }
        }

        void ImportCalli(int token)
        {
            throw new NotImplementedException($"{nameof(ImportCalli)} not implemented");
        }

        void ImportLdFtn(int token, ILOpcode opCode)
        {
            MethodDesc type = ResolveMethodToken(token);

            StackValue thisPtr = new StackValue();

            if (opCode == ILOpcode.ldvirtftn)
                thisPtr = Pop();

            // TODO

            throw new NotImplementedException($"{nameof(ImportLdFtn)} not implemented");
        }

        void ImportLoadInt(long value, StackValueKind kind)
        {
            Push(StackValue.CreatePrimitive(kind));
        }

        void ImportLoadFloat(double value)
        {
            Push(StackValue.CreatePrimitive(StackValueKind.Float));
        }

        void ImportLoadNull()
        {
            Push(StackValue.CreateObjRef(null));
        }

        void ImportReturn()
        {


#if false
    // 'this' must be init before return
    if (m_verTrackObjCtorInitState)
        Verify(vstate->isThisPublishable(), MVER_E_THIS_UNINIT_RET);

    // review : already in verifyreturnflow
    if (region)
    {
        switch (RgnGetRegionType(region))
        {
            case ReaderBaseNS::RGN_FILTER:
                BADCODE(MVER_E_RET_FROM_FIL);
                break;
            case ReaderBaseNS::RGN_TRY:
                BADCODE(MVER_E_RET_FROM_TRY);
                break;
            case ReaderBaseNS::RGN_FAULT:
            case ReaderBaseNS::RGN_FINALLY:
            case ReaderBaseNS::RGN_MEXCEPT:
            case ReaderBaseNS::RGN_MCATCH:
                BADCODE(MVER_E_RET_FROM_HND);
                break;
            case ReaderBaseNS::RGN_ROOT:
                break;
            default:
                VASSERT(UNREACHED);
                break;
        }
    }

    m_jitInfo->getMethodSig(getCurrentMethodHandle(), &sig);

    // Now get the return type and convert it to our format.
    corType = sig.retType;

    expectedStack = 0;
    if (corType != CORINFO_TYPE_VOID)
    {
        Verify(vstate->stackLevel() > 0, MVER_E_RET_MISSING);
        Verify(vstate->stackLevel() == 1, MVER_E_RET_EMPTY);

        vertype tiVal = vstate->impStackTop(0);
        vertype tiDeclared = verMakeTypeInfo(corType, sig.retTypeClass);

        VerifyCompatibleWith(tiVal, tiDeclared.NormaliseForStack());
        Verify((!verIsByRefLike(tiDeclared)) || verIsSafeToReturnByRef(tiVal), MVER_E_RET_PTR_TO_STACK);
        expectedStack=1;
    }
    else
    {
        Verify(vstate->stackLevel() == 0, MVER_E_RET_VOID);
    }

    if (expectedStack == 1)
        vstate->pop();
    else
        VASSERT(!expectedStack);
#endif

            // TODO
        }

        void ImportFallthrough(BasicBlock next)
        {
            StackValue[] entryStack = next.EntryStack;

            if (entryStack != null)
            {
                FatalCheck(entryStack.Length == _stackTop, VerifierError.PathStackDepth);

                for (int i = 0; i < entryStack.Length; i++)
                {
                    // TODO: Do we need to allow conversions?
                    FatalCheck(entryStack[i].Kind == _stack[i].Kind, VerifierError.PathStackUnexpected, entryStack[i], _stack[i]);
                    
                    if (entryStack[i].Type != _stack[i].Type)
                    {
                        // if we have two object references and one of them has a null type, then this is no error (see test Branching.NullConditional_Valid)
                        if (_stack[i].Kind == StackValueKind.ObjRef && entryStack[i].Type != null && _stack[i].Type != null)
                            FatalCheck(false, VerifierError.PathStackUnexpected, entryStack[i], _stack[i]);
                    }
                }
            }
            else
            {
                entryStack = (_stackTop != 0) ? new StackValue[_stackTop] : s_emptyStack;

                for (int i = 0; i < entryStack.Length; i++)
                    entryStack[i] = _stack[i];

                next.EntryStack = entryStack;
            }

            MarkBasicBlock(next);
        }

        void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            for (int i = 0; i < jmpDelta.Length; i++)
            {
                BasicBlock target = _basicBlocks[jmpBase + jmpDelta[i]];
                ImportFallthrough(target);
            }

            if (fallthrough != null)
                ImportFallthrough(fallthrough);

            throw new NotImplementedException($"{nameof(ImportSwitchJump)} not implemented");
        }

        void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            switch (opcode)
            {
                case ILOpcode.br:
                    break;
                case ILOpcode.brfalse:
                case ILOpcode.brtrue:
                    {
                        StackValue value = Pop();
                        Check(value.Kind >= StackValueKind.Int32 && value.Kind <= StackValueKind.NativeInt || value.Kind == StackValueKind.ObjRef || value.Kind == StackValueKind.ByRef, VerifierError.StackUnexpected);
                    }
                    break;
                case ILOpcode.beq:
                case ILOpcode.bge:
                case ILOpcode.bgt:
                case ILOpcode.ble:
                case ILOpcode.blt:
                case ILOpcode.bne_un:
                case ILOpcode.bge_un:
                case ILOpcode.bgt_un:
                case ILOpcode.ble_un:
                case ILOpcode.blt_un:
                    {
                        StackValue value1 = Pop();
                        StackValue value2 = Pop();

                        CheckIsComparable(value1, value2, opcode);
                    }
                    break;
                default:
                    Debug.Assert(false, "Unexpected branch opcode");
                    break;
            }

            ImportFallthrough(target);

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        void ImportBinaryOperation(ILOpcode opcode)
        {
            var op1 = Pop();
            var op2 = Pop();

            switch (opcode)
            {
                case ILOpcode.add:
                case ILOpcode.sub:
                case ILOpcode.mul:
                case ILOpcode.div:
                case ILOpcode.rem:
                    CheckIsNumeric(op1);
                    CheckIsNumeric(op2);
                    break;
                case ILOpcode.and:
                case ILOpcode.or:
                case ILOpcode.xor:
                case ILOpcode.div_un:
                case ILOpcode.rem_un:
                case ILOpcode.add_ovf:
                case ILOpcode.add_ovf_un:
                case ILOpcode.mul_ovf:
                case ILOpcode.mul_ovf_un:
                case ILOpcode.sub_ovf:
                case ILOpcode.sub_ovf_un:
                    CheckIsInteger(op1);
                    CheckIsInteger(op2);
                    break;
            }

            // StackValueKind is carefully ordered to make this work
            StackValue result = (op1.Kind > op2.Kind) ? op1 : op2;

            if ((op1.Kind != op2.Kind) && (result.Kind != StackValueKind.NativeInt))
            {
                VerificationError(VerifierError.StackUnexpected, op2, op1);
            }

            // The one exception from the above rule
            if ((result.Kind == StackValueKind.ByRef) &&
                    (opcode == ILOpcode.sub || opcode == ILOpcode.sub_ovf || opcode == ILOpcode.sub_ovf_un))
            {
                result = StackValue.CreatePrimitive(StackValueKind.NativeInt);
            }

            Push(result);
        }

        void ImportShiftOperation(ILOpcode opcode)
        {
            var shiftBy = Pop();
            var toBeShifted = Pop();

            Check(shiftBy.Kind == StackValueKind.Int32 || shiftBy.Kind == StackValueKind.NativeInt, VerifierError.StackUnexpected, shiftBy);
            CheckIsInteger(toBeShifted);

            Push(StackValue.CreatePrimitive(toBeShifted.Kind));
        }

        void ImportCompareOperation(ILOpcode opcode)
        {
            var value1 = Pop();
            var value2 = Pop();

            CheckIsComparable(value1, value2, opcode);

            Push(StackValue.CreatePrimitive(StackValueKind.Int32));
        }

        void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            var value = Pop();

            CheckIsNumeric(value);

            Push(StackValue.CreateFromType(GetWellKnownType(wellKnownType)));
        }

        void ImportLoadField(int token, bool isStatic)
        {
            ClearPendingPrefix(Prefix.Unaligned);
            ClearPendingPrefix(Prefix.Volatile);

            var field = ResolveFieldToken(token);

            if (isStatic)
            {
                Check(field.IsStatic, VerifierError.ExpectedStaticField);
            }
            else
            {
                var owningType = field.OwningType;

                // Note that even if the field is static, we require that the this pointer
                // satisfy the same constraints as a non-static field  This happens to
                // be simpler and seems reasonable
                var actualThis = Pop();
                if (actualThis.Kind == StackValueKind.ValueType)
                    actualThis = StackValue.CreateByRef(actualThis.Type);

                var declaredThis = owningType.IsValueType ?
                    StackValue.CreateByRef(owningType) : StackValue.CreateObjRef(owningType);

                CheckIsAssignable(actualThis, declaredThis);               
            }

            Push(StackValue.CreateFromType(field.FieldType));
        }

        void ImportAddressOfField(int token, bool isStatic)
        {
            var field = ResolveFieldToken(token);

            if (isStatic)
            {
                Check(field.IsStatic, VerifierError.ExpectedStaticField);
            }
            else
            {
                var owningType = field.OwningType;

                // Note that even if the field is static, we require that the this pointer
                // satisfy the same constraints as a non-static field  This happens to
                // be simpler and seems reasonable
                var actualThis = Pop();
                if (actualThis.Kind == StackValueKind.ValueType)
                    actualThis = StackValue.CreateByRef(actualThis.Type);

                var declaredThis = owningType.IsValueType ?
                    StackValue.CreateByRef(owningType) : StackValue.CreateObjRef(owningType);

                CheckIsAssignable(actualThis, declaredThis);
            }

            Push(StackValue.CreateByRef(field.FieldType));
        }

        void ImportStoreField(int token, bool isStatic)
        {
            ClearPendingPrefix(Prefix.Unaligned);
            ClearPendingPrefix(Prefix.Volatile);

            var value = Pop();

            var field = ResolveFieldToken(token);

            if (isStatic)
            {
                Check(field.IsStatic, VerifierError.ExpectedStaticField);
            }
            else
            {
                var owningType = field.OwningType;

                // Note that even if the field is static, we require that the this pointer
                // satisfy the same constraints as a non-static field  This happens to
                // be simpler and seems reasonable
                var actualThis = Pop();
                if (actualThis.Kind == StackValueKind.ValueType)
                    actualThis = StackValue.CreateByRef(actualThis.Type);

                var declaredThis = owningType.IsValueType ?
                    StackValue.CreateByRef(owningType) : StackValue.CreateObjRef(owningType);

                CheckIsAssignable(actualThis, declaredThis);
            }

            CheckIsAssignable(value, StackValue.CreateFromType(field.FieldType));
        }

        void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        void ImportLoadIndirect(TypeDesc type)
        {
            ClearPendingPrefix(Prefix.Unaligned);
            ClearPendingPrefix(Prefix.Volatile);

            var address = Pop();
            CheckIsByRef(address);

            if (type == null)
            {
                CheckIsObjRef(address.Type);
                type = address.Type;
            }
            else
            {
                CheckIsAssignable(address.Type, type);
            }
            Push(StackValue.CreateFromType(type));
        }

        void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        void ImportStoreIndirect(TypeDesc type)
        {
            ClearPendingPrefix(Prefix.Unaligned);
            ClearPendingPrefix(Prefix.Volatile);

            if (type == null)
                type = GetWellKnownType(WellKnownType.Object);

            var value = Pop();
            var address = Pop();

            Check(!address.IsReadOnly, VerifierError.ReadOnlyIllegalWrite);

            CheckIsByRef(address);

            var typeVal = StackValue.CreateFromType(type);
            var addressVal = StackValue.CreateFromType(address.Type);
            if (!value.IsNullReference)
                CheckIsAssignable(typeVal, addressVal);

            CheckIsAssignable(value, typeVal);
        }

        void ImportThrow()
        {
            var value = Pop();

            if (value.Kind != StackValueKind.ObjRef)
            {
                VerificationError(VerifierError.StackObjRef);
            }            
        }

        void ImportLoadString(int token)
        {
            object tokenObj = _methodIL.GetObject(token);
            Check(tokenObj is String, VerifierError.StringOperand);

            Push(StackValue.CreateObjRef(_typeSystemContext.GetWellKnownType(WellKnownType.String)));
        }

        void ImportInitObj(int token)
        {
            var type = ResolveTypeToken(token);

            var value = Pop();

            Check(value.Kind == StackValueKind.ByRef, VerifierError.StackByRef, value);

            CheckIsAssignable(value, StackValue.CreateByRef(type));
        }

        void ImportBox(int token)
        {
            var type = ResolveTypeToken(token);

            var value = Pop();

            var targetType = StackValue.CreateFromType(type);

            // TODO: Change the error message to say "cannot box byref" instead
            Check(!IsByRefLike(targetType), VerifierError.ExpectedValClassObjRefVariable, targetType);

#if false
            VerifyIsBoxable(tiBox);

            VerifyAndReportFound(m_jitInfo->satisfiesClassConstraints(clsHnd),
                                 tiBox,
                                 MVER_E_UNSATISFIED_BOX_OPERAND);  //"boxed type has unsatisfied class constraints"); 

            //Check for access to the type.
            verCheckClassAccess(pResolvedToken);
#endif

            CheckIsAssignable(value, targetType);

            // for nullable<T> we push T
            var typeForBox = type.IsNullable ? type.Instantiation[0] : type;

            // even if type is a value type we want the ref
            Push(StackValue.CreateObjRef(typeForBox));
        }

        static bool IsOffsetContained(int offset, int start, int length)
        {
            return start <= offset && offset < start + length;
        }

        void ImportLeave(BasicBlock target)
        {
            EmptyTheStack();

            MarkBasicBlock(target);
            // TODO
        }

        void ImportEndFinally()
        {
            Check(_currentBasicBlock.HandlerIndex.HasValue, VerifierError.Endfinally);
            Check(_exceptionRegions[_currentBasicBlock.HandlerIndex.Value].ILRegion.Kind == ILExceptionRegionKind.Finally ||
                _exceptionRegions[_currentBasicBlock.HandlerIndex.Value].ILRegion.Kind == ILExceptionRegionKind.Fault, VerifierError.Endfinally);

            EmptyTheStack();
        }

        void ImportNewArray(int token)
        {
            var elementType = ResolveTypeToken(token);

            var length = Pop();

            Check(!IsByRefLike(StackValue.CreateFromType(elementType)), VerifierError.ArrayByRef);

            Push(StackValue.CreateObjRef(elementType.Context.GetArrayType(elementType)));
        }

        void ImportLoadElement(int token)
        {
            ImportLoadElement(ResolveTypeToken(token));
        }

        void ImportLoadElement(TypeDesc elementType)
        {
            var index = Pop();
            var array = Pop();

            CheckIsIndex(index);
            CheckIsArray(array);

            if (array.Type != null)
            {
                var actualElementType = ((ArrayType)array.Type).ElementType;

                if (elementType != null)
                {
                    CheckIsArrayElementCompatibleWith(actualElementType, elementType);
                }
                else
                {
                    elementType = actualElementType;
                    CheckIsObjRef(elementType);
                }
            }

            Push(StackValue.CreateFromType(elementType));
        }

        void ImportStoreElement(int token)
        {
            ImportStoreElement(ResolveTypeToken(token));
        }

        void ImportStoreElement(TypeDesc elementType)
        {
            var value = Pop();
            var index = Pop();
            var array = Pop();

            CheckIsIndex(index);
            CheckIsArray(array);

            if (array.Type != null)
            {
                var actualElementType = ((ArrayType)array.Type).ElementType;

                if (elementType != null)
                {
                    CheckIsArrayElementCompatibleWith(actualElementType, elementType);
                }
                else
                {
                    elementType = actualElementType;
                    CheckIsObjRef(elementType);
                }
            }

            if (elementType != null)
            {
                CheckIsAssignable(value, StackValue.CreateFromType(elementType));
            }
        }

        void ImportAddressOfElement(int token)
        {
            var elementType = ResolveTypeToken(token);

            var index = Pop();
            var array = Pop();

            CheckIsIndex(index);
            CheckIsArray(array);

            if (array.Type != null)
            {
                var actualElementType = ((ArrayType)array.Type).ElementType;

                CheckIsPointerElementCompatibleWith(actualElementType, elementType);
            }

            Push(StackValue.CreateByRef(elementType, HasPendingPrefix(Prefix.ReadOnly)));
            ClearPendingPrefix(Prefix.ReadOnly);
        }

        void ImportLoadLength()
        {
            var array = Pop();

            CheckIsArray(array);

            Push(StackValue.CreatePrimitive(StackValueKind.NativeInt));
        }

        void ImportUnaryOperation(ILOpcode opCode)
        {
            var operand = Pop();

            switch (opCode)
            {
                case ILOpcode.neg:
                    CheckIsNumeric(operand);
                    break;
                case ILOpcode.not:
                    CheckIsInteger(operand);
                    break;
                default:
                    Debug.Assert(false, "Unexpected branch opcode");
                    break;
            }

            Push(StackValue.CreatePrimitive(operand.Kind));
        }

        void ImportCpOpj(int token)
        {
            var type = ResolveTypeToken(token);

            var src = Pop();
            var dst = Pop();

            Check(src.Kind == StackValueKind.ByRef, VerifierError.StackByRef, src);
            Check(dst.Kind == StackValueKind.ByRef, VerifierError.StackByRef, dst);

            // TODO !!!
            // CheckIsAssignable(src.Type, type);
            // CheckIsAssignable(type, dst.Type);
        }

        void ImportUnbox(int token, ILOpcode opCode)
        {
            var type = ResolveTypeToken(token);

            var value = Pop();

            if (opCode == ILOpcode.unbox_any)
            {
                Push(StackValue.CreateFromType(type));
            }
            else
            {
                Check(type.IsValueType, VerifierError.ValueTypeExpected);

                Push(StackValue.CreateByRef(type ,true));
            }
        }

        void ImportCkFinite()
        {
            var value = Pop();

            Check(value.Kind == StackValueKind.Float, VerifierError.ExpectedFloatType);

            Push(value);
        }

        void ImportLdToken(int token)
        {
            Object obj = ResolveToken(token);

            WellKnownType handleKind;

            if (obj is TypeDesc)
            {
                handleKind = WellKnownType.RuntimeTypeHandle;
            }
            else
            if (obj is MethodDesc)
            {
                handleKind = WellKnownType.RuntimeMethodHandle;
            }
            else
            if (obj is FieldDesc)
            {
                handleKind = WellKnownType.RuntimeFieldHandle;
            }
            else
            {
                throw new BadImageFormatException("Invalid token");
            }

            var handleType = _typeSystemContext.GetWellKnownType(handleKind);

            Push(StackValue.CreateValueType(handleType));
        }

        void ImportLocalAlloc()
        {
            Unverifiable();

            var size = Pop();

            CheckIsInteger(size);

            Push(StackValue.CreatePrimitive(StackValueKind.NativeInt));
        }

        void ImportEndFilter()
        {
            Check(_currentBasicBlock.FilterIndex.HasValue, VerifierError.Endfilter);
            Check(_currentOffset == _exceptionRegions[_currentBasicBlock.FilterIndex.Value].ILRegion.HandlerOffset, VerifierError.Endfilter);

            var result = Pop();
            Check(result.Kind == StackValueKind.Int32, VerifierError.StackUnexpected);
            Check(_stackTop == 0, VerifierError.EndfilterStack);
        }

        void ImportCpBlk()
        {
            ClearPendingPrefix(Prefix.Unaligned);
            ClearPendingPrefix(Prefix.Volatile);

            Unverifiable();

            var size = Pop();
            var srcaddr = Pop();
            var dstaddr = Pop();

            CheckIsInteger(size);

            // TODO: Validate srcaddr, dstaddr
        }

        void ImportInitBlk()
        {
            ClearPendingPrefix(Prefix.Unaligned);
            ClearPendingPrefix(Prefix.Volatile);

            Unverifiable();

            var size = Pop();
            var value = Pop();
            var addr = Pop();

            CheckIsInteger(size);
            CheckIsInteger(value);

            // TODO: Validate addr
        }

        void ImportRethrow()
        {
            if (_currentBasicBlock.HandlerIndex.HasValue)
            {
                var eR = _exceptionRegions[_currentBasicBlock.HandlerIndex.Value].ILRegion;

                //in case a simple catch
                if (eR.Kind == ILExceptionRegionKind.Catch)
                {
                    return;
                }

                //in case a filter make sure rethrow is within the handler
                if (eR.Kind == ILExceptionRegionKind.Filter &&
                    _currentOffset >= eR.HandlerOffset &&
                    _currentOffset <= eR.HandlerOffset + eR.HandlerLength)
                {
                    return;
                }
            }

            VerificationError(VerifierError.Rethrow);
        }

        void ImportSizeOf(int token)
        {
            var type = ResolveTypeToken(token);

            Push(StackValue.CreatePrimitive(StackValueKind.Int32));
        }

        //
        // Prefix
        //

        void ImportUnalignedPrefix(byte alignment)
        {
            CheckPendingPrefix(_pendingPrefix & ~Prefix.Volatile);
            _pendingPrefix |= Prefix.Unaligned;
        }

        void ImportVolatilePrefix()
        {
            CheckPendingPrefix(_pendingPrefix & ~Prefix.Unaligned);
            _pendingPrefix |= Prefix.Volatile;
        }

        void ImportTailPrefix()
        {
            CheckPendingPrefix(_pendingPrefix);
            _pendingPrefix |= Prefix.Tail;
        }

        void ImportConstrainedPrefix(int token)
        {
            CheckPendingPrefix(_pendingPrefix);
            _pendingPrefix |= Prefix.Constrained;

            _constrained = ResolveTypeToken(token);
        }

        void ImportNoPrefix(byte mask)
        {
            Unverifiable();

            CheckPendingPrefix(_pendingPrefix);
            _pendingPrefix |= Prefix.No;
        }

        void ImportReadOnlyPrefix()
        {
            CheckPendingPrefix(_pendingPrefix);
            _pendingPrefix |= Prefix.ReadOnly;
        }

        void CheckPendingPrefix(Prefix mask)
        {
            if (mask == 0) return;

            //illegal to stack prefixes
            Check((mask & Prefix.Unaligned) == 0, VerifierError.Unaligned);
            Check((mask & Prefix.Tail) == 0, VerifierError.TailCall);
            Check((mask & Prefix.Volatile) == 0, VerifierError.Volatile);
            Check((mask & Prefix.ReadOnly) == 0, VerifierError.ReadOnly);
            Check((mask & Prefix.Constrained) == 0, VerifierError.Constrained);
        }

        bool HasPendingPrefix(Prefix prefix)
        {
            return (_pendingPrefix & prefix) != 0;
        }

        void ClearPendingPrefix(Prefix prefix)
        {
            _pendingPrefix &= ~prefix;
        }

        //
        // Deprecated
        //

        void ImportArgList()
        {
            throw new PlatformNotSupportedException("RuntimeArgumentHandle not supported in .NET Core");
        }

        void ImportRefAnyType()
        {
            throw new PlatformNotSupportedException("TypedReference not supported in .NET Core");
        }

        void ImportMkRefAny(int token)
        {
            throw new PlatformNotSupportedException("TypedReference not supported in .NET Core");
        }

        void ImportRefAnyVal(int token)
        {
            throw new PlatformNotSupportedException("TypedReference not supported in .NET Core");
        }

        private void ReportInvalidBranchTarget(int targetOffset)
        {
            throw new NotImplementedException();
        }

        private void ReportFallthroughAtEndOfMethod()
        {
            throw new NotImplementedException();
        }

        private void ReportInvalidInstruction(ILOpcode opcode)
        {
            throw new NotImplementedException();
        }
    }
}
