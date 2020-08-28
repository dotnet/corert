// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;

using ILCompiler.DependencyAnalysis;
using LLVMSharp.Interop;

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

                ilImporter = new ILImporter(compilation, method, methodIL, mangledName, methodCodeNodeNeedingCode is WebAssemblyUnboxingThunkNode);

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
                ilImporter.CreateEHData(methodCodeNodeNeedingCode);
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
        static LLVMValueRef CxaBeginCatchFunction = default(LLVMValueRef);
        static LLVMValueRef CxaEndCatchFunction = default(LLVMValueRef);
        static LLVMValueRef RhpThrowEx = default(LLVMValueRef);
        static LLVMValueRef RhpCallCatchFunclet = default(LLVMValueRef);
        static LLVMValueRef LlvmCatchFunclet = default(LLVMValueRef);
        static LLVMValueRef LlvmFilterFunclet = default(LLVMValueRef);
        static LLVMValueRef LlvmFinallyFunclet = default(LLVMValueRef);
        static LLVMValueRef NullRefFunction = default(LLVMValueRef);
        static LLVMValueRef CkFinite32Function = default(LLVMValueRef);
        static LLVMValueRef CkFinite64Function = default(LLVMValueRef);
        public static LLVMValueRef GxxPersonality = default(LLVMValueRef);
        public static LLVMTypeRef GxxPersonalityType = default(LLVMTypeRef);

        internal static LLVMValueRef MakeFatPointer(LLVMBuilderRef builder, LLVMValueRef targetLlvmFunction, WebAssemblyCodegenCompilation compilation)
        {
            var asInt = builder.BuildPtrToInt(targetLlvmFunction, LLVMTypeRef.Int32, "toInt");
            return builder.BuildBinOp(LLVMOpcode.LLVMOr, asInt, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)compilation.TypeSystemContext.Target.FatFunctionPointerOffset, false), "makeFat");
        }

        private static IList<string> GetParameterNamesForMethod(MethodDesc method)
        {
            // TODO: The uses of this method need revision. The right way to get to this info is from
            //       a MethodIL. For declarations, we don't need names.
            method = method.GetTypicalMethodDefinition();
            var ecmaMethod = method as EcmaMethod;
            if (ecmaMethod != null && ecmaMethod.Module.PdbReader != null)
            {
                List<string> parameterNames = new List<string>(new EcmaMethodDebugInformation(ecmaMethod).GetParameterNames());

                // Return the parameter names only if they match the method signature
                if (parameterNames.Count != 0)
                {
                    var methodSignature = method.Signature;
                    int argCount = methodSignature.Length;
                    if (!methodSignature.IsStatic)
                        argCount++;

                    if (parameterNames.Count == argCount)
                        return parameterNames;
                }
            }

            return null;
        }

        static void BuildCatchFunclet(LLVMModuleRef module, LLVMTypeRef[] funcletArgTypes)
        {
            LlvmCatchFunclet = module.AddFunction("LlvmCatchFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, funcletArgTypes, false));
            var block = LlvmCatchFunclet.AppendBasicBlock("Catch");
            LLVMBuilderRef funcletBuilder = Context.CreateBuilder();
            funcletBuilder.PositionAtEnd( block);

            LLVMValueRef leaveToILOffset = funcletBuilder.BuildCall(LlvmCatchFunclet.GetParam(0), new LLVMValueRef[] { LlvmCatchFunclet.GetParam(1) }, "callCatch");
            funcletBuilder.BuildRet(leaveToILOffset);
            funcletBuilder.Dispose();
        }

        static void BuildFilterFunclet(LLVMModuleRef module, LLVMTypeRef[] funcletArgTypes)
        {
            LlvmFilterFunclet = module.AddFunction("LlvmFilterFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32,  funcletArgTypes, false));
            var block = LlvmFilterFunclet.AppendBasicBlock("Filter");
            LLVMBuilderRef funcletBuilder = Context.CreateBuilder();
            funcletBuilder.PositionAtEnd(block);

            LLVMValueRef filterResult = funcletBuilder.BuildCall(LlvmFilterFunclet.GetParam(0), new LLVMValueRef[] { LlvmFilterFunclet.GetParam(1) }, "callFilter");
            funcletBuilder.BuildRet(filterResult);
            funcletBuilder.Dispose();
        }

        static void BuildFinallyFunclet(LLVMModuleRef module)
        {
            LlvmFinallyFunclet = module.AddFunction("LlvmFinallyFunclet", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void,
                new LLVMTypeRef[]
                {
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)}, false), 0), // finallyHandler
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), // shadow stack
                }, false));
            var block = LlvmFinallyFunclet.AppendBasicBlock("Finally");
            LLVMBuilderRef funcletBuilder = Context.CreateBuilder();
            funcletBuilder.PositionAtEnd(block);

            var finallyFunclet = LlvmFinallyFunclet.GetParam(0);
            var castShadowStack = LlvmFinallyFunclet.GetParam(1);

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(castShadowStack);

            funcletBuilder.BuildCall(finallyFunclet, llvmArgs.ToArray(), string.Empty);
            funcletBuilder.BuildRetVoid();
            funcletBuilder.Dispose();
        }
    }
}
