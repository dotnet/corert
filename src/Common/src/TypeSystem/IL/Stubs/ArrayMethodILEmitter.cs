﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs
{
    internal class ArrayMethodILEmitter : ILEmitter
    {
        ArrayMethod _method;
        TypeDesc _elementType;
        int _rank;

        int _helperFieldToken;

        public ArrayMethodILEmitter(ArrayMethod method)
        {
            _method = method;

            ArrayType arrayType = (ArrayType)method.OwningType;
            _rank = arrayType.Rank;
            _elementType = arrayType.ElementType;
        }

        void EmitLoadInteriorAddress(ILCodeStream codeStream, int offset)
        {
            // This helper field is needed to generate proper GC tracking. There is no direct way
            // to create interior pointer. 
            if (_helperFieldToken == 0)
                _helperFieldToken = NewToken(_method.Context.GetWellKnownType(WellKnownType.Object).GetField("m_pEEType"));

            codeStream.EmitLdArg(0); // this
            codeStream.Emit(ILOpcode.ldflda, _helperFieldToken);
            codeStream.EmitLdc(offset);
            codeStream.Emit(ILOpcode.add);
        }

        public MethodIL EmitIL()
        {
            switch (_method.Kind)
            {
                case ArrayMethodKind.Get:
                case ArrayMethodKind.Set:
                case ArrayMethodKind.Address:
                    EmitILForAccessor();
                    break;

                case ArrayMethodKind.Ctor:
                    // .ctor is implemented as a JIT helper and the JIT shouldn't be asking for the IL.
                default:
                    // Asking for anything else is invalid.
                    throw new InvalidOperationException();
            }

            return Link();
        }

        void EmitILForAccessor()
        {
            Debug.Assert(_rank > 1);

            var codeStream = NewCodeStream();

            var int32Type = _method.Context.GetWellKnownType(WellKnownType.Int32);

            var totalLocalNum = NewLocal(int32Type);
            var lengthLocalNum = NewLocal(int32Type);

            int pointerSize = _method.Context.Target.PointerSize;

            var rangeExceptionLabel = NewCodeLabel();

            // TODO: type check

            for (int i = 0; i < _rank; i++)
            {
                // The first two fields are EEType pointer and total length. Lengths for each dimension follows.
                int lengthOffset = (2 * pointerSize + i * int32Type.GetElementSize());

                EmitLoadInteriorAddress(codeStream, lengthOffset);
                codeStream.Emit(ILOpcode.ldind_i4);
                codeStream.EmitStLoc(lengthLocalNum);

                codeStream.EmitLdArg(i + 1);

                // Compare with length
                codeStream.Emit(ILOpcode.dup);
                codeStream.EmitLdLoc(lengthLocalNum);
                codeStream.Emit(ILOpcode.bge_un, rangeExceptionLabel);

                // Add to the running total if we have one already
                if (i > 0)
                {
                    codeStream.EmitLdLoc(totalLocalNum);
                    codeStream.EmitLdLoc(lengthLocalNum);
                    codeStream.Emit(ILOpcode.mul);
                    codeStream.Emit(ILOpcode.add);
                }
                codeStream.EmitStLoc(totalLocalNum);
            }

            // Compute element offset
            // TODO: This leaves unused space for lower bounds to match CoreCLR...
            int firstElementOffset = (2 * pointerSize + 2 * _rank * int32Type.GetElementSize());
            EmitLoadInteriorAddress(codeStream, firstElementOffset);

            codeStream.EmitLdLoc(totalLocalNum);

            int elementSize = _elementType.GetElementSize();
            if (elementSize != 1)
            {
                codeStream.EmitLdc(elementSize);
                codeStream.Emit(ILOpcode.mul);
            }
            codeStream.Emit(ILOpcode.add);

            switch (_method.Kind)
            {
                case ArrayMethodKind.Get:
                    codeStream.Emit(ILOpcode.ldobj, NewToken(_elementType));
                    break;

                case ArrayMethodKind.Set:
                    codeStream.EmitLdArg(_rank + 1);
                    codeStream.Emit(ILOpcode.stobj, NewToken(_elementType));
                    break;

                case ArrayMethodKind.Address:
                    break;
            }

            codeStream.Emit(ILOpcode.ret);

            codeStream.EmitLdc(0);
            codeStream.EmitLabel(rangeExceptionLabel); // Assumes that there is one "int" pushed on the stack
            codeStream.Emit(ILOpcode.pop);

            MethodDesc throwHelper = _method.Context.GetHelperEntryPoint("ArrayMethodILHelpers", "ThrowIndexOutOfRangeException");
            codeStream.Emit(ILOpcode.call, NewToken(throwHelper));
            codeStream.Emit(ILOpcode.ret);
#if false
            if (typeMismatchExceptionLabel != null)
            {
                var tokTypeMismatchExcepCtor = GetToken(GetException(kArrayTypeMismatchException).GetDefaultConstructor());

                codeStream.EmitLabel(typeMismatchExceptionLabel);
                codeStream.Emit(ILOpcode.newobj, tokTypeMismatchExcepCtor, 0);
                codeStream.Emit(ILOpcode.throw_);
            }
#endif
        }
    }
}
