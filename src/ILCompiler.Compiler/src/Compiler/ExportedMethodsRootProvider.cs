// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    /// <summary>
    /// Computes a set of roots based on managed and unmanaged methods exported from a module.
    /// </summary>
    public class ExportedMethodsRootProvider : ICompilationRootProvider
    {
        private EcmaModule _module;

        public ExportedMethodsRootProvider(EcmaModule module)
        {
            _module = module;
        }

        public IEnumerable<EcmaMethod> ExportedMethods
        {
            get
            {
                foreach (var type in _module.GetAllTypes())
                {
                    foreach (var method in type.GetMethods())
                    {
                        EcmaMethod ecmaMethod = (EcmaMethod)method;
                        if (ecmaMethod.IsRuntimeExport || ecmaMethod.IsNativeCallable)
                            yield return ecmaMethod;
                    }
                }
            }
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            foreach (var ecmaMethod in ExportedMethods)
            {
                if (ecmaMethod.IsRuntimeExport)
                {
                    string runtimeExportName = ecmaMethod.GetRuntimeExportName();
                    if (runtimeExportName != null)
                        rootProvider.AddCompilationRoot((MethodDesc)ecmaMethod, "Runtime export", runtimeExportName);
                }
                else if (ecmaMethod.IsNativeCallable)
                {
                    string nativeCallableExportName = ecmaMethod.GetNativeCallableExportName();
                    if (nativeCallableExportName != null)
                        rootProvider.AddCompilationRoot((MethodDesc)ecmaMethod, "Native callable", nativeCallableExportName);
                }
            }
        }
    }
}
