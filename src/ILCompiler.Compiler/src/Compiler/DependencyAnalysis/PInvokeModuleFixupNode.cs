﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a single PInvoke ModuleFixupCell as defined in the core library.
    /// </summary>
    public class PInvokeModuleFixupNode : ObjectNode, ISymbolDefinitionNode
    {
        public string _moduleName;

        public PInvokeModuleFixupNode(string moduleName)
        {
            _moduleName = moduleName;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__nativemodule_");
            sb.Append(_moduleName);
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);

            ISymbolNode nameSymbol = factory.ConstantUtf8String(_moduleName);

            //
            // Emit a ModuleFixupCell struct
            //

            builder.EmitZeroPointer();
            builder.EmitPointerReloc(nameSymbol);

            return builder.ToObjectData();
        }

        protected internal override int ClassCode => 159930099;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return string.Compare(_moduleName, ((PInvokeModuleFixupNode)other)._moduleName);
        }
    }
}
