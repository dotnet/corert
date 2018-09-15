// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.IL;
using Internal.Runtime.CallInterceptor;
using Internal.TypeSystem;

namespace Internal.Runtime.Interpreter
{
    internal unsafe class ILInterpreter
    {
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly TypeSystemContext _context;
        private readonly LowLevelStack<StackItem> _stack;

        private CallInterceptorArgs _callInterceptorArgs;

        public LowLevelStack<StackItem> EvaluationStack => _stack;

        public TypeSystemContext TypeSystemContext => _context;

        public ILInterpreter(TypeSystemContext context, MethodDesc method, MethodIL methodIL)
        {
            _context = context;
            _method = method;
            _methodIL = methodIL;
            _stack = new LowLevelStack<StackItem>();
        }

        public void InterpretMethod(ref CallInterceptorArgs callInterceptorArgs)
        {
            _callInterceptorArgs = callInterceptorArgs;
            ILImporter importer = new ILImporter(this, _method, _methodIL);
            importer.Interpret();
        }

        public void SetReturnValue<T>(T value)
        {
            _callInterceptorArgs.ArgumentsAndReturnValue.SetVar<T>(0, value);
        }
    }
}
