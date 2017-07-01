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
    public abstract class InteropStubManager
    {
        private readonly CompilationModuleGroup _compilationModuleGroup;
        private readonly CompilerTypeSystemContext _typeSystemContext;
        protected ModuleDesc _interopModule;
        private const string _interopModuleName = "System.Private.Interop";

        public InteropStateManager InteropStateManager
        {
            get;
        }

        public InteropStubManager(CompilationModuleGroup compilationModuleGroup, CompilerTypeSystemContext typeSystemContext, InteropStateManager interopStateManager)
        {
            _compilationModuleGroup = compilationModuleGroup;
            _typeSystemContext = typeSystemContext;
            InteropStateManager = interopStateManager;

            // Note: interopModule might be null if we're building with a class library that doesn't support rich interop
            _interopModule = typeSystemContext.GetModuleForSimpleName(_interopModuleName, false);
        }


        public abstract void AddDependeciesDueToPInvoke(ref DependencyList dependencies, NodeFactory factory, MethodDesc method);
        
        public abstract void AddInterestingInteropConstructedTypeDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type);
        

        /// <summary>
        /// For Marshal generic APIs(eg. Marshal.StructureToPtr<T>, GetFunctionPointerForDelegate) we add
        /// the generic parameter as dependencies so that we can generate runtime data for them
        /// </summary>
        public abstract void AddMarshalAPIsGenericDependencies(ref DependencyList dependencies, NodeFactory factory, MethodDesc method);

        public abstract void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode);        
    }
}
