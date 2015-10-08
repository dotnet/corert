// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;

namespace Internal.IL.Stubs
{
    internal class ArrayMethodILEmitter : ILEmitter
    {
        ArrayMethod _method;
        TypeDesc _elementType;
        int _rank;

        public ArrayMethodILEmitter(ArrayMethod method)
        {
            _method = method;

            ArrayType arrayType = (ArrayType)method.OwningType;
            _rank = arrayType.Rank;
            _elementType = arrayType.ElementType;
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
                default:
                    // EmitILForCtor();
                    break;
            }

            return Link();
        }

#if false
        void EmitILForCtor()
        {
            var codeStream = NewCodeStream();

            // TODO: generate IL to check for negative bounds and throw OverflowException

            codeStream.EmitLdArg(0);

            // Compute size of the backing array
            codeStream.EmitLdArg(1);
            for (int i = 2; i <= _rank; i++)
            {
                codeStream.EmitLdArg(i);
                codeStream.Emit(ILOpcode.mul_ovf);
            }

            // Allocate backing array and store it in the private field
            codeStream.Emit(ILOpcode.newarr, NewToken(_elementType));
            FieldDesc backingArrayField = _backingType.GetField("m_array");
            codeStream.Emit(ILOpcode.stfld, NewToken(backingArrayField));

            // Store bounds
            for (int i = 1; i <= _rank; i++)
            {
                FieldDesc upperBoundField = _backingType.GetField("m_upperBound" + i);

                codeStream.EmitLdArg(0);
                codeStream.EmitLdArg(i);
                codeStream.Emit(ILOpcode.stfld, NewToken(upperBoundField));
            }

            codeStream.Emit(ILOpcode.ret);
        }

        void EmitComputeIndex(ILCodeStream codeStream)
        {
            // Compute index into the backing array
            for (int i = 1; i < _rank; i++)
            {
                FieldDesc upperBoundField = _backingType.GetField("m_upperBound" + _rank);
                codeStream.EmitLdArg(0);
                codeStream.Emit(ILOpcode.ldfld, NewToken(upperBoundField));
                for (int j = _rank - 1; j > i; j--)
                {
                    upperBoundField = _backingType.GetField("m_upperBound" + j);
                    codeStream.EmitLdArg(0);
                    codeStream.Emit(ILOpcode.ldfld, NewToken(upperBoundField));
                    codeStream.Emit(ILOpcode.mul);
                }

                codeStream.EmitLdArg(i);
                codeStream.Emit(ILOpcode.mul);

                if (i != 1)
                {
                    codeStream.Emit(ILOpcode.add);
                }
            }

            codeStream.EmitLdArg(_rank);
            codeStream.Emit(ILOpcode.add);
        }


        MethodIL EmitILForSet()
        {
            var codeStream = NewCodeStream();

            // TODO: generate IL to check bounds

            codeStream.EmitLdArg(0);
            FieldDesc backingArrayField = _backingType.GetField("m_array");
            codeStream.Emit(ILOpcode.ldfld, NewToken(backingArrayField));

            EmitComputeIndex(codeStream);

            // Store value at index
            codeStream.EmitLdArg(_rank + 1);
            codeStream.Emit(ILOpcode.stelem, NewToken(_elementType));

            codeStream.Emit(ILOpcode.ret);

            return Link();
        }

        void EmitILForGet()
        {
            var codeStream = NewCodeStream();

            // TODO: generate IL to check bounds

            codeStream.EmitLdArg(0);
            FieldDesc backingArrayField = _backingType.GetField("m_array");
            codeStream.Emit(ILOpcode.ldfld, NewToken(backingArrayField));

            EmitComputeIndex(codeStream);

            // Load value at index
            codeStream.Emit(ILOpcode.ldelem, NewToken(_elementType));

        }

        void EmitILForAddress()
        {
            // TODO
            throw new NotImplementedException();
        }
#endif

        int _helperFieldToken;

        // Create interior pointer at given offset from 'this'
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

        void EmitILForAccessor()
        {
            Debug.Assert(_rank > 1);

            var codeStream = NewCodeStream();

            var int32Type = _method.Context.GetWellKnownType(WellKnownType.Int32);

            var totalLocalNum = NewLocal(int32Type);
            var lengthLocalNum = NewLocal(int32Type);

            int pointerSize = _method.Context.Target.PointerSize;

            for (int i = 0; i < _rank; i++)
            {
                // The first two fields are EEType pointer and total length. Lengths for each dimension follows.
                int lengthOffset = (2 * pointerSize + i * sizeof(Int32));

                EmitLoadInteriorAddress(codeStream, lengthOffset);
                codeStream.Emit(ILOpcode.ldind_i4);
                codeStream.EmitStLoc(lengthLocalNum);

                codeStream.EmitLdArg(i + 1);

#if false
                // TODO: generate IL to check bounds
                // Compare with length
                codeStream.Emit(ILOpcode.dup);
                codeStream.EmitLdLoc(lengthLocalNum);
                codeStream.Emit(ILOpcode.bge_un, rangeExceptionLabel1);
#endif

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
            int firstElementOffset = (2 * pointerSize + 2 * _rank * sizeof(Int32));
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
                    codeStream.EmitLdArg(_rank);
                    codeStream.Emit(ILOpcode.stobj, NewToken(_elementType));
                    break;

                case ArrayMethodKind.Address:
                    break;
            }

            codeStream.Emit(ILOpcode.ret);

#if false
            codeStream.EmitLdc(0);
            codeStream.EmitLabel(rangeExceptionLabel1); // Assumes that there is one "int" pushed on the stack
            codeStream.Emit(ILOpcode.pop);

            var tokIndexOutOfRangeCtorExcep = GetToken(GetException(kIndexOutOfRangeException).GetDefaultConstructor());
            codeStream.EmitLabel(rangeExceptionLabel);
            codeStream.Emit(ILOpcode.newobj, tokIndexOutOfRangeCtorExcep, 0);
            codeStream.Emit(ILOpcode.throw_);

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
