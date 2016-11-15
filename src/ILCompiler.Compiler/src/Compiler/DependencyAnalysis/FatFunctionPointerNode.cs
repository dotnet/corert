﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a fat function pointer - a data structure that captures a pointer to a canonical
    /// method body along with the instantiation context the canonical body requires.
    /// Pointers to these structures can be created by e.g. ldftn/ldvirtftn of a method with a canonical body.
    /// </summary>
    public class FatFunctionPointerNode : ObjectNode, IMethodNode
    {
        public FatFunctionPointerNode(MethodDesc methodRepresented)
        {
            // We should not create these for methods that don't have a canonical method body
            Debug.Assert(methodRepresented.GetCanonMethodTarget(CanonicalFormKind.Specific) != methodRepresented);

            Method = methodRepresented;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__fatpointer_").Append(NodeFactory.NameMangler.GetMangledMethodName(Method));
        }
        public int Offset => 0;

        public MethodDesc Method { get; }

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory) => true;

        protected override string GetName() => this.GetMangledName();

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList result = new DependencyList();
            result.Add(new DependencyListEntry(factory.ShadowConcreteMethod(Method), "Method represented"));
            return result;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            var builder = new ObjectDataBuilder(factory);

            // These need to be aligned the same as methods because they show up in same contexts
            builder.RequireAlignment(factory.Target.MinimumFunctionAlignment);

            builder.DefinedSymbols.Add(this);

            MethodDesc canonMethod = Method.GetCanonMethodTarget(CanonicalFormKind.Specific);

            // Pointer to the canonical body of the method
            builder.EmitPointerReloc(factory.MethodEntrypoint(canonMethod));

            // Find out what's the context to use
            ISymbolNode contextParameter;
            if (canonMethod.RequiresInstMethodDescArg())
            {
                contextParameter = factory.MethodGenericDictionary(Method);
            }
            else
            {
                Debug.Assert(canonMethod.RequiresInstMethodTableArg());

                // Ask for a constructed type symbol because we need the vtable to get to the dictionary
                contextParameter = factory.ConstructedTypeSymbol(Method.OwningType);
            }

            // The next entry is a pointer to the pointer to the context to be used for the canonical method
            // TODO: in multi-module, this points to the import cell, and is no longer this weird pointer
            builder.EmitPointerReloc(factory.Indirection(contextParameter));
            
            return builder.ToObjectData();
        }
    }
}
