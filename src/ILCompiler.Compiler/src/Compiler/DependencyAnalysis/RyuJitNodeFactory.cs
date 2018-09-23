﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class RyuJitNodeFactory : NodeFactory
    {
        public RyuJitNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, MetadataManager metadataManager,
            InteropStubManager interopStubManager, NameMangler nameMangler, VTableSliceProvider vtableSliceProvider, DictionaryLayoutProvider dictionaryLayoutProvider)
            : base(context, compilationModuleGroup, metadataManager, interopStubManager, nameMangler, new LazyGenericsDisabledPolicy(), vtableSliceProvider, dictionaryLayoutProvider, new ExternSymbolsImportedNodeProvider())
        {
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.IsInternalCall)
            {
                if (TypeSystemContext.IsSpecialUnboxingThunkTargetMethod(method))
                {
                    return MethodEntrypoint(TypeSystemContext.GetRealSpecialUnboxingThunkTargetMethod(method));
                }
                else if (method.IsArrayAddressMethod())
                {
                    return MethodEntrypoint(((ArrayType)method.OwningType).GetArrayMethod(ArrayMethodKind.AddressWithHiddenArg));
                }
                else if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
                {
                    return new RuntimeImportMethodNode(method);
                }
            }

            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                return new MethodCodeNode(method);
            }
            else
            {
                return _importedNodeProvider.ImportedMethodCodeNode(this, method, false);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            Debug.Assert(!method.Signature.IsStatic);

            if (method.IsCanonicalMethod(CanonicalFormKind.Specific) && !method.HasInstantiation)
            {
                // Unboxing stubs to canonical instance methods need a special unboxing stub that unboxes
                // 'this' and also provides an instantiation argument (we do a calling convention conversion).
                // We don't do this for generic instance methods though because they don't use the EEType
                // for the generic context anyway.
                return new MethodCodeNode(TypeSystemContext.GetSpecialUnboxingThunk(method, TypeSystemContext.GeneratedAssembly));
            }
            else
            {
                // Otherwise we just unbox 'this' and don't touch anything else.
                return new UnboxingStubNode(method, Target);
            }
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            return new ReadyToRunHelperNode(this, helperCall.HelperId, helperCall.Target);
        }
    }
}
