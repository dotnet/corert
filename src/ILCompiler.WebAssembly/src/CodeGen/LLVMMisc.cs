using System;
using System.Collections.Generic;
using System.Text;
using LLVMSharp;

namespace ILCompiler.CodeGen
{
    class LLVMMisc
    {
        public static LLVMBool False { get; } = new LLVMBool(0);

        public static LLVMBool True { get; } = new LLVMBool(1);
    }
}
