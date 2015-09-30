// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;

namespace Internal.IL.Stubs
{
    internal class ArrayMethodILEmitter : ILEmitter
    {
        ArrayMethod _method;
        TypeDesc _elementType;
        TypeDesc _backingType;
        int _rank;

        public ArrayMethodILEmitter(ArrayMethod method)
        {
            _method = method;

            ArrayType arrayType = (ArrayType)method.OwningType;
            _rank = arrayType.Rank;
            _elementType = arrayType.ElementType;

            var systemModule = ((EcmaType)method.Context.GetWellKnownType(WellKnownType.Object)).Module;

            _backingType = systemModule.GetType("System", "MDArrayRank" + _rank.ToString() + "`1").MakeInstantiatedType(
                    new Instantiation(new TypeDesc[] { _elementType })
                );
        }

        public MethodIL EmitIL()
        {
            switch (_method.Kind)
            {
                case ArrayMethodKind.Get:
                    EmitILForGet();
                    break;
                case ArrayMethodKind.Set:
                    EmitILForSet();
                    break;
                case ArrayMethodKind.Address:
                    EmitILForAddress();
                    break;
                default:
                    EmitILForCtor();
                    break;
            }

            return Link();
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

            codeStream.Emit(ILOpcode.ret);
        }

        void EmitILForAddress()
        {
            // TODO
            throw new NotImplementedException();
        }
    }
}
