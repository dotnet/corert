// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                int delta = (_method.Signature.IsStatic ? 1 : 2);
                LocalVariableType[] localVariableTypes = new LocalVariableType[_method.Signature.Length + delta];
                localVariableTypes[0] = new LocalVariableType(_method.Signature.ReturnType.GetRuntimeTypeHandle(), false, _method.Signature.ReturnType.IsByRef);

                if (!_method.Signature.IsStatic)
                {
                    localVariableTypes[1] = new LocalVariableType(_method.OwningType.GetRuntimeTypeHandle(), false, _method.OwningType.IsByRef);
                }

                for (int i = 0; i < _method.Signature.Length; i++)
                {
                    var argument = _method.Signature[i];
                    localVariableTypes[i + delta] = new LocalVariableType(argument.GetRuntimeTypeHandle(), false, argument.IsByRef);
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
                    localVariableTypes[i] = new LocalVariableType(variable.Type.GetRuntimeTypeHandle(), variable.IsPinned, variable.Type.IsByRef);
                }

                return localVariableTypes;
            }
        }

        public override void ThunkExecute(ref CallInterceptorArgs callInterceptorArgs)
        {
            ILInterpreter interpreter = new ILInterpreter(_context, _method, _methodIL);
            interpreter.InterpretMethod(ref callInterceptorArgs);
        }
    }
}
