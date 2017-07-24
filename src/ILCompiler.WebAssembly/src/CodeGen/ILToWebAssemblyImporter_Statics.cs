// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler;
using ILCompiler.Compiler.CppCodeGen;

using ILCompiler.DependencyAnalysis;

namespace Internal.IL
{
    internal partial class ILImporter
    {
        public static void CompileMethod(WebAssemblyCodegenCompilation compilation, MethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            compilation.Logger.Writer.WriteLine("Compiling " + method.ToString());
            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                throw new NotImplementedException();
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

            try
            {
                var ilImporter = new ILImporter(compilation, method, methodIL);

                CompilerTypeSystemContext typeSystemContext = compilation.TypeSystemContext;

                MethodDebugInformation debugInfo = compilation.GetDebugInfo(methodIL);

               /* if (!compilation.Options.HasOption(CppCodegenConfigProvider.NoLineNumbersString))*/
                {
                    IEnumerable<ILSequencePoint> sequencePoints = debugInfo.GetSequencePoints();
                    /*if (sequencePoints != null)
                        ilImporter.SetSequencePoints(sequencePoints);*/
                }

                IEnumerable<ILLocalVariable> localVariables = debugInfo.GetLocalVariables();
                /*if (localVariables != null)
                    ilImporter.SetLocalVariables(localVariables);*/

                IEnumerable<string> parameters = GetParameterNamesForMethod(method);
                /*if (parameters != null)
                    ilImporter.SetParameterNames(parameters);*/

                ilImporter.Import();
            }
            catch (Exception e)
            {
                compilation.Logger.Writer.WriteLine(e.Message + " (" + method + ")");

                throw new NotImplementedException();
                //methodCodeNodeNeedingCode.SetCode(sb.ToString(), Array.Empty<Object>());
            }
        }

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
