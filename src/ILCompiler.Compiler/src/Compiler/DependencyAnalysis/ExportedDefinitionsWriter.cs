// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal static class ExportedDefinitionsWriter
    {
        private static string _exportsFilePath;
        private static string _moduleName;
        private static TargetDetails _targetDetails;

        private static string GetExportsFileExtenstion()
            => _targetDetails.IsWindows ? ".def" : ".exports";

        private static void BuildExportsFileInfo(string outputFile)
        {
            string directory = Path.GetDirectoryName(outputFile);
            string filename = Path.GetFileNameWithoutExtension(outputFile);
            _moduleName = filename;
            _exportsFilePath = Path.Combine(directory, filename + GetExportsFileExtenstion());
        }

        public static void EmitExportedSymbols(string outputFile, NodeFactory factory)
        {
            _targetDetails = factory.Target;
            BuildExportsFileInfo(outputFile);
            var nativeCallables = factory.NodeAliases.Where(n => n.Key is IMethodNode)
                                    .Where(n => (n.Key as IMethodNode).Method.IsNativeCallable);

            WriteExportedSymbolsToFile(nativeCallables.Select(n => n.Value));
        }

        private static void WriteExportedSymbolsToFile(IEnumerable<string> exportNames)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (_targetDetails.OperatingSystem == TargetOS.Windows)
            {
                stringBuilder.Append("LIBRARY   ");
                stringBuilder.AppendLine(_moduleName.ToUpper());

                stringBuilder.AppendLine("EXPORTS");
                foreach (var exportName in exportNames)
                    stringBuilder.AppendLine("   " + exportName);
            }
            else if (_targetDetails.OperatingSystem == TargetOS.OSX)
            {
                stringBuilder.Append("# Module: ");
                stringBuilder.AppendLine(_moduleName);
                foreach (var exportName in exportNames)
                    stringBuilder.AppendLine("_" + exportName);
            }

            File.WriteAllText(_exportsFilePath, stringBuilder.ToString());
        }
    }
}