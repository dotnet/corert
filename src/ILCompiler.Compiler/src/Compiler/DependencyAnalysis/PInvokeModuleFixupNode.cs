// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

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

        public int Offset
        {
            get
            {
                return 0;
            }
        }

        public string MangledName
        {
            get
            {
                return String.Concat("__nativemodule_", _moduleName);
            }
        }

        public override string GetName()
        {
            return MangledName;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return ObjectNodeSection.DataSection;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

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
