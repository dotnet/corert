using System;
using System.Collections.Generic;
using System.Text;
using Internal.IL;
using Internal.Runtime.CallInterceptor;
using Internal.TypeSystem;

namespace Internal.Runtime.Interpreter
{
    class ILInterpreter
    {
        private MethodDesc _method;
        private MethodIL _methodIL;

        public ILInterpreter(MethodDesc method, MethodIL methodIL)
        {
            _method = method;
            _methodIL = methodIL;
        }

        public void Interpret(ref CallInterceptorArgs callInterceptorArgs)
        {

        }
    }
}
