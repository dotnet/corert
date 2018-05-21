using System;
using Internal.Runtime.TypeLoader;

namespace Internal.Runtime.Interpreter
{
    class InterpreterExecutionStrategy : MethodExecutionStrategy
    {
        public override IntPtr OnEntryPoint(MethodEntrypointPtr entrypointInfo, IntPtr callerArgumentsInfo)
        {
            throw new NotImplementedException();
        }
    }
}
