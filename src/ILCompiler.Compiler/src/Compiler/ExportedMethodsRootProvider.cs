// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text;

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
        private string _exportsFile;

        public ExportedMethodsRootProvider(EcmaModule module, string exportsFile)
        {
            _module = module;
            _methods = new List<EcmaMethod>();
            _exportsFile = exportsFile;
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
                            _methods.Add(ecmaMethod);
                            rootProvider.AddCompilationRoot(method, "Native callable", nativeCallableExportName);
                        }
                    }
                }
            }

            // Only emit exported methods for user supplied assemblies
            // _exportsFile is null when we're not doing a shared library build
            if (_module != _module.Context.SystemModule && _exportsFile != null)
                EmitExportedMethods();
        }

        private void EmitExportedMethods()
        {
            string moduleName = Path.GetFileNameWithoutExtension(_exportsFile);
            StringBuilder stringBuilder = new StringBuilder();

            if (_module.Context.Target.IsWindows)
            {
                stringBuilder.Append("LIBRARY   ");
                stringBuilder.AppendLine(moduleName.ToUpper());

                stringBuilder.AppendLine("EXPORTS");
                foreach (var method in _methods)
                    stringBuilder.AppendLine("   " + method.GetNativeCallableExportName());
            }
            else
            {
                stringBuilder.Append("# Module: ");
                stringBuilder.AppendLine(moduleName);
                foreach (var method in _methods)
                    stringBuilder.AppendLine("_" + method.GetNativeCallableExportName());
            }

            File.WriteAllText(_exportsFile, stringBuilder.ToString());
        }
    }
}
