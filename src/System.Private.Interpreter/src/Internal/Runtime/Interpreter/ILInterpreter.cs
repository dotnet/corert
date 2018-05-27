using System;
using System.Collections.Generic;
using System.Text;
using Internal.IL;
using Internal.Runtime.CallInterceptor;
using Internal.TypeSystem;

namespace Internal.Runtime.Interpreter
{
    internal class ILInterpreter
    {
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly LowLevelStack<object> _stack;

        public ILInterpreter(MethodDesc method, MethodIL methodIL)
        {
            _method = method;
            _methodIL = methodIL;
            _stack = new LowLevelStack<object>();
        }

        public void Interpret(ref CallInterceptorArgs callInterceptorArgs)
        {
            ILDisassembler disassembler = new ILDisassembler(_methodIL);
            ILInstruction instruction = disassembler.GetNextILInstruction();
            while (instruction != null)
            {
                ILEvaluation.EvaluateInstruction(instruction, _stack, ref callInterceptorArgs);
                instruction = disassembler.GetNextILInstruction();
            }
        }
    }
}
