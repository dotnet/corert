// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a single PInvoke MethodFixupCell as defined in the core library.
    /// </summary>
    public class PInvokeMethodFixupNode : ObjectNode, ISymbolNode
    {
        private string _moduleName;
        private string _entryPointName;

        public PInvokeMethodFixupNode(string moduleName, string entryPointName)
        {
            _moduleName = moduleName;
            _entryPointName = entryPointName;
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
                return String.Concat("__pinvoke_", _moduleName, "__", _entryPointName);
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
            // Emit a MethodFixupCell struct
            //

            builder.EmitZeroPointer();

            int entryPointBytesCount = Encoding.UTF8.GetByteCount(_entryPointName);
            byte[] entryPointNameBytes = new byte[entryPointBytesCount + 1];
            Encoding.UTF8.GetBytes(_entryPointName, 0, _entryPointName.Length, entryPointNameBytes, 0);

            builder.EmitPointerReloc(factory.ReadOnlyDataBlob("__pinvokename_" + _entryPointName, entryPointNameBytes, 1));
            builder.EmitPointerReloc(factory.PInvokeModuleFixup(_moduleName));

            return builder.ToObjectData();
        }
    }
}
