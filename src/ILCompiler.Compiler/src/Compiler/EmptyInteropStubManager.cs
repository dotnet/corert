// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;

using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing stub methods for interop
    /// </summary>
    public sealed class EmptyInteropStubManager : InteropStubManager
    {
        public EmptyInteropStubManager(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext, InteropStateManager interopStateManager) :
            base(compilationModuleGroup, typeSystemContext, interopStateManager)
        {
        }

        public override void AddDependeciesDueToPInvoke(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
        }

        public override void AddInterestingInteropConstructedTypeDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
        }
 
        public override void AddMarshalAPIsGenericDependencies(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
        }

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
        }
    }
}