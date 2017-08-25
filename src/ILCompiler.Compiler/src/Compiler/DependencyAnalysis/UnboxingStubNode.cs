// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an unboxing stub that supports calling instance methods on boxed valuetypes.
    /// </summary>
    public partial class UnboxingStubNode : ObjectNode, IMethodNode, IExportableSymbolNode
    {
        // Section name has to be alphabetically less than the ending UnboxingStubsRegionNode node, and larger than
        // the begining UnboxingStubsRegionNode node, in order to have proper delimiters to the begining/ending of the
        // stubs region, and refer to it using a ReadyToRunSection type.
        static readonly string WindowsSectionName = ".unbox$M";
        static readonly string UnixSectionName = "__unbox";

        private readonly TargetDetails _targetDetails;

        public MethodDesc Method { private set; get; }

        public override ObjectNodeSection Section
        {
            get
            {
                string sectionName = _targetDetails.IsWindows ? WindowsSectionName : UnixSectionName;
                return new ObjectNodeSection(sectionName, SectionType.Executable);
            }
        }
        public override bool IsShareable => true;
        public override bool StaticDependenciesAreComputed => true;
        public int Offset => 0;

        public bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsMethod(Method);

        public UnboxingStubNode(MethodDesc target, TargetDetails targetDetails)
        {
            Debug.Assert(target.GetCanonMethodTarget(CanonicalFormKind.Specific) == target);
            Debug.Assert(target.OwningType.IsValueType);
            Method = target;
            _targetDetails = targetDetails;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("unbox_").Append(nameMangler.GetMangledMethodName(Method));
        }

        public static string GetMangledName(NameMangler nameMangler, MethodDesc method)
        {
            return "unbox_" + nameMangler.GetMangledMethodName(method);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            switch (factory.Target.Architecture)
            {
                case TargetArchitecture.X64:
                    X64.X64Emitter x64Emitter = new X64.X64Emitter(factory, relocsOnly);
                    EmitUnboxingStubCode(factory, ref x64Emitter);
                    return x64Emitter.Builder.ToObjectData();

                case TargetArchitecture.X86:
                    X86.X86Emitter x86Emitter = new X86.X86Emitter(factory, relocsOnly);
                    EmitUnboxingStubCode(factory, ref x86Emitter);
                    return x86Emitter.Builder.ToObjectData();

                case TargetArchitecture.ARM:
                case TargetArchitecture.ARMEL:
                    ARM.ARMEmitter armEmitter = new ARM.ARMEmitter(factory, relocsOnly);
                    EmitUnboxingStubCode(factory, ref armEmitter);
                    return armEmitter.Builder.ToObjectData();

                default:
                    throw new NotImplementedException();
            }
        }
    }

    //
    // On Windows, we need to create special start/stop sections, in order to group all the unboxing stubs and
    // have delimiters accessible through R2R sections. On Linux/Apple, the linker provides special names to the 
    // begining and end of sections already.
    //
    public class WindowsUnboxingStubsRegionNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly bool _isEndSymbol;

        public override ObjectNodeSection Section => new ObjectNodeSection(".unbox" + (_isEndSymbol? "Z" : "A"), SectionType.Executable);
        public override bool IsShareable => true;
        public override bool StaticDependenciesAreComputed => true;
        public int Offset => 0;

        public WindowsUnboxingStubsRegionNode(bool isEndSymbol)
        {
            _isEndSymbol = isEndSymbol;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("unbox_stubs_region_" + (_isEndSymbol ? "End" : "Start"));
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            Debug.Assert(factory.Target.IsWindows);

            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialAlignment(factory.Target.MinimumFunctionAlignment);
            objData.AddSymbol(this);

            return objData.ToObjectData();
        }
    }
}
