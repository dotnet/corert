﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler.DependencyAnalysis
{
    public abstract class ModulesSectionNode : ObjectNode, ISymbolNode
    {
        // Each compilation unit produces one module. When all compilation units are linked
        // together in multifile mode, the runtime needs to get list of modules present
        // in the final binary. This list is created via a special .modules section that
        // contains list of pointers to all module headers.
        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.NameMangler.CompilationUnitPrefix + "__Module";
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.RequirePointerAlignment();
            objData.DefinedSymbols.Add(this);
            objData.EmitPointerReloc(factory.ReadyToRunHeader);

            return objData.ToObjectData();
        }
    }

    public class WindowsModulesSectionNode : ModulesSectionNode
    {
        public static readonly string SectionName = ".modules$I";

        public override ObjectNodeSection Section
        {
            get
            {
                return new ObjectNodeSection(SectionName, SectionType.ReadOnly);
            }
        }
    }

    public class UnixModulesSectionNode : ModulesSectionNode
    {

        public static readonly string SectionName = "__modules";

        public override ObjectNodeSection Section
        {
            get
            {
                return new ObjectNodeSection(SectionName, SectionType.ReadOnly);
            }
        }
    }
}
