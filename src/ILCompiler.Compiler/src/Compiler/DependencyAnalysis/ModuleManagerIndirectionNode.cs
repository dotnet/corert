// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using System;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis
{
    class ModuleManagerIndirectionNode : ObjectNode, ISymbolNode
    {
        public string MangledName
        {
            get
            {
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__module_manager_indirection";
            }
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public int Offset
        {
            get
            {
                return 0;
            }
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
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.DefinedSymbols.Add(this);
            objData.RequirePointerAlignment();
            objData.EmitZeroPointer();
            return objData.ToObjectData();
        }
    }
}
