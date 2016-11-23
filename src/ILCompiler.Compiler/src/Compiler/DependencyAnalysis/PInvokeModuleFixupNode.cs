﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb, string compilationUnitPrefix)
        {
            sb.Append("__nativemodule_");
            sb.Append(_moduleName);
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName() => this.GetMangledName();

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);
            builder.DefinedSymbols.Add(this);

            ISymbolNode nameSymbol = factory.Target.IsWindows ?
                factory.ConstantUtf16String(_moduleName) :
                factory.ConstantUtf8String(_moduleName);

            //
            // Emit a ModuleFixupCell struct
            //

            builder.EmitZeroPointer();
            builder.EmitPointerReloc(nameSymbol);

            return builder.ToObjectData();
        }
    }
}
