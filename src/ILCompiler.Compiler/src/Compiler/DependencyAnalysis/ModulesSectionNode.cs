// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class ModulesSectionNode : ObjectNode, ISymbolNode
    {
        // Each compilation unit produces one module. When all compilation units are linked
        // together in multifile mode, the runtime needs to get list of modules present
        // in the final binary. This list is created via a special .modules section that
        // contains list of pointers to all module headers.
        public static readonly string WindowsSectionName = ".modules$I";
        public static readonly string UnixSectionName = "__modules";

        private TargetDetails _target;

        public ModulesSectionNode(TargetDetails target)
        {
            _target = target;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                string sectionName = _target.IsWindows ? WindowsSectionName : UnixSectionName;
                return new ObjectNodeSection(sectionName, SectionType.ReadOnly);
            }
        }

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
                return NodeFactory.CompilationUnitPrefix + "__Module";
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
}
