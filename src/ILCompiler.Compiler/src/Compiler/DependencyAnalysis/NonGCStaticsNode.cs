// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using FatFunctionPointerConstants = Internal.Runtime.FatFunctionPointerConstants;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a node with non-GC static data associated with a type, along
    /// with it's class constructor context. The non-GC static data region shall be prefixed
    /// with the class constructor context if the type has a class constructor that
    /// needs to be triggered before the type members can be accessed.
    /// </summary>
    internal class NonGCStaticsNode : ObjectNode, ISymbolNode
    {
        private MetadataType _type;

        public NonGCStaticsNode(MetadataType type, NodeFactory factory)
        {
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Specific));
            _type = type;
        }

        protected override string GetName() => this.GetMangledName();

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__NonGCStaticBase_").Append(NodeFactory.NameMangler.GetMangledTypeName(_type));
        }
        public int Offset => 0;

        public MetadataType Type => _type;

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return factory.CompilationModuleGroup.ShouldShareAcrossModules(_type);
        }
        
        private static int GetClassConstructorContextSize(TargetDetails target)
        {
            // TODO: Assert that StaticClassConstructionContext type has the expected size
            //       (need to make it a well known type?)
            return target.PointerSize * 2;
        }

        public static int GetClassConstructorContextStorageSize(TargetDetails target, MetadataType type)
        {
            int alignmentRequired = Math.Max(type.NonGCStaticFieldAlignment, GetClassConstructorContextAlignment(target));
            return AlignmentHelper.AlignUp(GetClassConstructorContextSize(type.Context.Target), alignmentRequired);
        }

        private static int GetClassConstructorContextAlignment(TargetDetails target)
        {
            // TODO: Assert that StaticClassConstructionContext type has the expected alignment
            //       (need to make it a well known type?)
            return target.PointerSize;
        }

        public override bool StaticDependenciesAreComputed => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            if (factory.TypeSystemContext.HasEagerStaticConstructor(_type))
            {
                var result = new DependencyList();
                result.Add(factory.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
                return result;
            }

            return null;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory);

            // If the type has a class constructor, its non-GC statics section is prefixed  
            // by System.Runtime.CompilerServices.StaticClassConstructionContext struct.
            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                int alignmentRequired = Math.Max(_type.NonGCStaticFieldAlignment, GetClassConstructorContextAlignment(_type.Context.Target));
                int classConstructorContextStorageSize = GetClassConstructorContextStorageSize(factory.Target, _type);
                builder.RequireAlignment(alignmentRequired);
                
                Debug.Assert(classConstructorContextStorageSize >= GetClassConstructorContextSize(_type.Context.Target));

                // Add padding before the context if alignment forces us to do so
                builder.EmitZeros(classConstructorContextStorageSize - GetClassConstructorContextSize(_type.Context.Target));

                // Emit the actual StaticClassConstructionContext
                MethodDesc cctorMethod = _type.GetStaticConstructor();
                MethodDesc canonCctorMethod = cctorMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                if (cctorMethod != canonCctorMethod)
                    builder.EmitPointerReloc(factory.FatFunctionPointer(cctorMethod), FatFunctionPointerConstants.Offset);
                else
                    builder.EmitPointerReloc(factory.MethodEntrypoint(cctorMethod));
                builder.EmitZeroPointer();
            }
            else
            {
                builder.RequireAlignment(_type.NonGCStaticFieldAlignment);
            }

            builder.EmitZeros(_type.NonGCStaticFieldSize);
            builder.DefinedSymbols.Add(this);

            return builder.ToObjectData();
        }
    }
}