// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;
using ILCompiler.CodeGen;

using ILCompiler.DependencyAnalysis;
using LLVMSharp;

namespace Internal.IL
{
    internal partial class ILImporter
    {
        public static void CompileMethod(WebAssemblyCodegenCompilation compilation, WebAssemblyMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            if (compilation.Logger.IsVerbose)
            {
                string methodName = method.ToString();
                compilation.Logger.Writer.WriteLine("Compiling " + methodName);
            }

            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                methodCodeNodeNeedingCode.CompilationCompleted = true;
                //throw new NotImplementedException();
                //CompileExternMethod(methodCodeNodeNeedingCode, ((EcmaMethod)method).GetRuntimeImportName());
                //return;
            }

            if (method.IsRawPInvoke())
            {
                //CompileExternMethod(methodCodeNodeNeedingCode, method.GetPInvokeMethodMetadata().Name ?? method.Name);
                //return;
            }

            var methodIL = compilation.GetMethodIL(method);
            if (methodIL == null)
                return;

            ILImporter ilImporter = null;
            try
            {
                string mangledName;

                // TODO: Better detection of the StartupCodeMain method
                if (methodCodeNodeNeedingCode.Method.Signature.IsStatic && methodCodeNodeNeedingCode.Method.Name == "StartupCodeMain")
                {
                    mangledName = "StartupCodeMain";
                }
                else
                {
                    mangledName = compilation.NameMangler.GetMangledMethodName(methodCodeNodeNeedingCode.Method).ToString();
                }

                ilImporter = new ILImporter(compilation, method, methodIL, mangledName);

                CompilerTypeSystemContext typeSystemContext = compilation.TypeSystemContext;

                //MethodDebugInformation debugInfo = compilation.GetDebugInfo(methodIL);

               /* if (!compilation.Options.HasOption(CppCodegenConfigProvider.NoLineNumbersString))*/
                {
                    //IEnumerable<ILSequencePoint> sequencePoints = debugInfo.GetSequencePoints();
                    /*if (sequencePoints != null)
                        ilImporter.SetSequencePoints(sequencePoints);*/
                }

                //IEnumerable<ILLocalVariable> localVariables = debugInfo.GetLocalVariables();
                /*if (localVariables != null)
                    ilImporter.SetLocalVariables(localVariables);*/

                IEnumerable<string> parameters = GetParameterNamesForMethod(method);
                /*if (parameters != null)
                    ilImporter.SetParameterNames(parameters);*/

                ilImporter.Import();

                methodCodeNodeNeedingCode.CompilationCompleted = true;
            }
            catch (Exception e)
            {
                compilation.Logger.Writer.WriteLine(e.Message + " (" + method + ")");

                methodCodeNodeNeedingCode.CompilationCompleted = true;
//                methodCodeNodeNeedingCode.SetDependencies(ilImporter.GetDependencies());
                //throw new NotImplementedException();
                //methodCodeNodeNeedingCode.SetCode(sb.ToString(), Array.Empty<Object>());
            }

            // Uncomment the block below to get specific method failures when LLVM fails for cryptic reasons
#if false
            LLVMBool result = LLVM.VerifyFunction(ilImporter._llvmFunction, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            if (result.Value != 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error compliling {method.OwningType}.{method}");
                Console.ResetColor();
            }
#endif // false

            // Ensure dependencies show up regardless of exceptions to avoid breaking LLVM
            methodCodeNodeNeedingCode.SetDependencies(ilImporter.GetDependencies());
        }

        static LLVMValueRef DebugtrapFunction = default(LLVMValueRef);
        static LLVMValueRef TrapFunction = default(LLVMValueRef);
        static LLVMValueRef DoNothingFunction = default(LLVMValueRef);

        private static IEnumerable<string> GetParameterNamesForMethod(MethodDesc method)
        {
            // TODO: The uses of this method need revision. The right way to get to this info is from
            //       a MethodIL. For declarations, we don't need names.

            method = method.GetTypicalMethodDefinition();
            var ecmaMethod = method as EcmaMethod;
            if (ecmaMethod != null && ecmaMethod.Module.PdbReader != null)
            {
                return (new EcmaMethodDebugInformation(ecmaMethod)).GetParameterNames();
            }

            return null;
        }

    }
}
