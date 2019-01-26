// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.IL;
using Internal.Runtime.CallConverter;
using Internal.Runtime.CallInterceptor;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.Runtime.Interpreter
{
    public class InterpreterCallInterceptor : CallInterceptor.CallInterceptor
    {
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly TypeSystemContext _context;

        public InterpreterCallInterceptor(TypeSystemContext context, MethodDesc method) : base(false)
        {
            _context = context;
            _method = method;
            _methodIL = EcmaMethodIL.Create(method as EcmaMethod);
        }

        public override LocalVariableType[] ArgumentAndReturnTypes
        {
            get
            {
                int delta = 1;

                LocalVariableType[] localVariableTypes = new LocalVariableType[_method.Signature.Length + 1];
                localVariableTypes[0] = new LocalVariableType(GetRuntimeTypeHandleForUnknownType(_method.Signature.ReturnType), false, _method.Signature.ReturnType.IsByRef);

                if (!_method.Signature.IsStatic)
                {
                    localVariableTypes[1] = new LocalVariableType(GetRuntimeTypeHandleForUnknownType(_method.OwningType), false, _method.OwningType.IsByRef);
                    delta = 2;
                }

                for (int i = 0; i < _method.Signature.Length; i++)
                {
                    var argument = _method.Signature[i];
                    localVariableTypes[i + delta] = new LocalVariableType(GetRuntimeTypeHandleForUnknownType(argument), false, argument.IsByRef);
                }

                return localVariableTypes;
            }
        }

        public override CallingConvention CallingConvention
        {
            get
            {
                return _method.Signature.IsStatic ? CallingConvention.ManagedStatic : CallingConvention.ManagedInstance;
            }
        }

        public override LocalVariableType[] LocalVariableTypes
        {
            get
            {
                LocalVariableDefinition[] locals = _methodIL.GetLocals();
                LocalVariableType[] localVariableTypes = new LocalVariableType[locals.Length];
                for (int i = 0; i < locals.Length; i++)
                {
                    var variable = locals[i];
                    localVariableTypes[i] = new LocalVariableType(GetRuntimeTypeHandleForUnknownType(variable.Type), variable.IsPinned, variable.Type.IsByRef);
                }

                return localVariableTypes;
            }
        }

        public override void ThunkExecute(ref CallInterceptorArgs callInterceptorArgs)
        {
            ILInterpreter interpreter = new ILInterpreter(_context, _method, _methodIL);
            interpreter.InterpretMethod(ref callInterceptorArgs);
        }

        private RuntimeTypeHandle GetRuntimeTypeHandleForUnknownType(TypeDesc type)
        {
            RuntimeTypeHandle runtimeTypeHandle = type.RuntimeTypeHandle;
            if (runtimeTypeHandle.Value != IntPtr.Zero)
                return runtimeTypeHandle;

            switch (type.Category)
            {
                case TypeFlags.Void:
                case TypeFlags.Boolean:
                case TypeFlags.Char:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Single:
                case TypeFlags.Double:
                    // Primitive well known types should never have RuntimeTypeHandle with a zero value.
                    // If execution gets here, we have a problem!
                    Debug.Assert(false);
                    break;
                case TypeFlags.ValueType:
                    runtimeTypeHandle = _context.GetWellKnownType(WellKnownType.ValueType).RuntimeTypeHandle;
                    break;
                case TypeFlags.Enum:
                    runtimeTypeHandle = _context.GetWellKnownType(WellKnownType.Enum).RuntimeTypeHandle;
                    break;
                case TypeFlags.Nullable:
                    runtimeTypeHandle = _context.GetWellKnownType(WellKnownType.Nullable).RuntimeTypeHandle;
                    break;
                case TypeFlags.Class:
                case TypeFlags.Interface:
                    runtimeTypeHandle = _context.GetWellKnownType(WellKnownType.Object).RuntimeTypeHandle;
                    break;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    runtimeTypeHandle = _context.GetWellKnownType(WellKnownType.Array).RuntimeTypeHandle;
                    break;
                default:
                    // TODO: Support more complex argument and local variable types
                    throw new NotImplementedException();
            }

            return runtimeTypeHandle;
        }
    }
}
