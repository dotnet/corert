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
        private static string _defFilePath;
        private static string _moduleName;

        private static void BuildDefinitionFileInfo(string outputFile)
        {
            string directory = Path.GetDirectoryName(outputFile);
            string filename = Path.GetFileNameWithoutExtension(outputFile);
            _moduleName = filename;
            _defFilePath = Path.Combine(directory, filename + ".def");
        }

        public static void EmitExportedSymbols(string outputFile, NodeFactory factory)
        {
            BuildDefinitionFileInfo(outputFile);
            var nativeCallables = factory.NodeAliases.Where(n => n.Key is IMethodNode)
                                    .Where(n => (n.Key as IMethodNode).Method.IsNativeCallable);

            WriteToDefinitionFile(nativeCallables.Select(n => n.Value));
        }

        private static void WriteToDefinitionFile(IEnumerable<string> exportNames)
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append("LIBRARY   ");
            stringBuilder.AppendLine(_moduleName.ToUpper());

            stringBuilder.AppendLine("EXPORTS");
            foreach (var exportName in exportNames)
            {
                stringBuilder.Append("   ");
                stringBuilder.AppendLine(exportName);
            }

            File.WriteAllText(_defFilePath, stringBuilder.ToString());
        }
    }
}