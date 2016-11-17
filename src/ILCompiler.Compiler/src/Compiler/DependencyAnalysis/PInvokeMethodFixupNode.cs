// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;

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

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__pinvoke_");
            sb.Append(_moduleName);
            sb.Append("__");
            sb.Append(_entryPointName);
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
            // Emit a MethodFixupCell struct
            //

            builder.EmitZeroPointer();
            builder.EmitPointerReloc(factory.ConstantUtf8String(_entryPointName));
            builder.EmitPointerReloc(factory.PInvokeModuleFixup(_moduleName));

            return builder.ToObjectData();
        }
    }
}
