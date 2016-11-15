// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a single PInvoke ModuleFixupCell as defined in the core library.
    /// </summary>
    public class PInvokeModuleFixupNode : ObjectNode, ISymbolNode
    {
        public string _moduleName;

        public PInvokeModuleFixupNode(string moduleName)
        {
            _moduleName = moduleName;
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__nativemodule_");
            sb.Append(_moduleName);
        }
        public int Offset => 0;


        protected override string GetName() => this.GetMangledName();

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);
            builder.DefinedSymbols.Add(this);

            //
            // Emit a ModuleFixupCell struct
            //

            builder.EmitZeroPointer();

            Encoding encoding = factory.Target.IsWindows ? Encoding.Unicode : Encoding.UTF8;

            int moduleNameBytesCount = encoding.GetByteCount(_moduleName);
            byte[] moduleNameBytes = new byte[moduleNameBytesCount + 2];
            encoding.GetBytes(_moduleName, 0, _moduleName.Length, moduleNameBytes, 0);
            builder.EmitPointerReloc(factory.ReadOnlyDataBlob("__modulename_" + _moduleName, moduleNameBytes, 2));

            return builder.ToObjectData();
        }
    }
}
