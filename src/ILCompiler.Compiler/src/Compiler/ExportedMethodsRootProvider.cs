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
        private List<EcmaMethod> _methods;

        public ExportedMethodsRootProvider(EcmaModule module)
        {
            _module = module;
            _methods = new List<EcmaMethod>();
        }

        public IEnumerable<EcmaMethod> ExportedMethods
        {
            get
            {
                return _methods;
            }
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            foreach (var type in _module.GetAllTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    EcmaMethod ecmaMethod = (EcmaMethod)method;

                    if (ecmaMethod.IsRuntimeExport)
                    {
                        string runtimeExportName = ecmaMethod.GetRuntimeExportName();
                        if (runtimeExportName != null)
                            rootProvider.AddCompilationRoot(method, "Runtime export", runtimeExportName);
                    }

                    if (ecmaMethod.IsNativeCallable)
                    {
                        string nativeCallableExportName = ecmaMethod.GetNativeCallableExportName();

                        if (nativeCallableExportName != null)
                        {
                            if (ecmaMethod.Module != _module.Context.SystemModule)
                                _methods.Add(ecmaMethod);
                            rootProvider.AddCompilationRoot(method, "Native callable", nativeCallableExportName);
                        }
                    }
                }
            }
        }
    }
}
