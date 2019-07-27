﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;

using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;
using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;
using ReflectionMapBlob = Internal.Runtime.ReflectionMapBlob;

namespace ILCompiler
{
    /// <summary>
    /// This class is responsible for managing stub methods for interop
    /// </summary>
    public sealed class CompilerGeneratedInteropStubManager : InteropStubManager
    {
        private readonly PInvokeILEmitterConfiguration _pInvokeILEmitterConfiguration;
        internal readonly InteropStateManager _interopStateManager;

        public CompilerGeneratedInteropStubManager(InteropStateManager interopStateManager, PInvokeILEmitterConfiguration pInvokeILEmitterConfiguration)
        {
            _interopStateManager = interopStateManager;
            _pInvokeILEmitterConfiguration = pInvokeILEmitterConfiguration;
        }

        public override PInvokeILProvider CreatePInvokeILProvider()
        {
            return new PInvokeILProvider(_pInvokeILEmitterConfiguration, _interopStateManager);
        }
        
        public override void AddDependeciesDueToPInvoke(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            if (method.IsPInvoke)
            {
                dependencies = dependencies ?? new DependencyList();

                MethodSignature methodSig = method.Signature;
                AddParameterMarshallingDependencies(ref dependencies, factory, methodSig.ReturnType);

                for (int i = 0; i < methodSig.Length; i++)
                {
                    AddParameterMarshallingDependencies(ref dependencies, factory, methodSig[i]);
                }
            }

            if (method.HasInstantiation)
            {
                dependencies = dependencies ?? new DependencyList();
                AddMarshalAPIsGenericDependencies(ref dependencies, factory, method);
            }
        }

        private static void AddParameterMarshallingDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                dependencies.Add(factory.DelegateMarshallingData((DefType)type), "Delegate marshaling");
            }

            // struct may contain delegate fields, hence we need to add dependencies for it
            if (type.IsByRef)
                type = ((ParameterizedType)type).ParameterType;

            if (MarshalHelpers.IsStructMarshallingRequired(type))
            {
                dependencies.Add(factory.StructMarshallingData((DefType)type), "Struct marshalling");
            }
        }

        public override void AddInterestingInteropConstructedTypeDependencies(ref DependencyList dependencies, NodeFactory factory, TypeDesc type)
        {
            if (type.IsDelegate)
            {
                var delegateType = (MetadataType)type;
                if (delegateType.HasCustomAttribute("System.Runtime.InteropServices", "UnmanagedFunctionPointerAttribute"))
                {
                    dependencies = dependencies ?? new DependencyList();
                    dependencies.Add(factory.DelegateMarshallingData(delegateType), "Delegate marshalling");
                }
            }
        }

        /// <summary>
        /// For Marshal generic APIs(eg. Marshal.StructureToPtr<T>, GetFunctionPointerForDelegate) we add
        /// the generic parameter as dependencies so that we can generate runtime data for them
        /// </summary>
        public override void AddMarshalAPIsGenericDependencies(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            Debug.Assert(method.HasInstantiation);

            TypeDesc owningType = method.OwningType;
            MetadataType metadataType = owningType as MetadataType;
            if (metadataType != null && metadataType.Module == factory.TypeSystemContext.SystemModule)
            {
                if (metadataType.Name == "Marshal" && metadataType.Namespace == "System.Runtime.InteropServices")
                {
                    string methodName = method.Name;
                    if (methodName == "GetFunctionPointerForDelegate" ||
                        methodName == "GetDelegateForFunctionPointer" ||
                        methodName == "PtrToStructure" ||
                        methodName == "StructureToPtr" ||
                        methodName == "SizeOf" ||
                        methodName == "OffsetOf")
                    {
                        foreach (TypeDesc type in method.Instantiation)
                        {
                            dependencies = dependencies ?? new DependencyList();
                            if (type.IsDelegate)
                            {
                                dependencies.Add(factory.DelegateMarshallingData((DefType)type), "Delegate marshlling");
                            }
                            else if (MarshalHelpers.IsStructMarshallingRequired(type) || (methodName == "OffsetOf" && type is DefType))
                            {
                                dependencies.Add(factory.StructMarshallingData((DefType)type), "Struct marshalling");
                            }
                        }
                    }
                }
            }
        }

        public override void AddToReadyToRunHeader(ReadyToRunHeaderNode header, NodeFactory nodeFactory, ExternalReferencesTableNode commonFixupsTableNode)
        {
            var delegateMapNode = new DelegateMarshallingStubMapNode(commonFixupsTableNode, _interopStateManager);
            header.Add(MetadataManager.BlobIdToReadyToRunSection(ReflectionMapBlob.DelegateMarshallingStubMap), delegateMapNode, delegateMapNode, delegateMapNode.EndSymbol);

            var structMapNode = new StructMarshallingStubMapNode(commonFixupsTableNode, _interopStateManager);
            header.Add(MetadataManager.BlobIdToReadyToRunSection(ReflectionMapBlob.StructMarshallingStubMap), structMapNode, structMapNode, structMapNode.EndSymbol);
        }
    }
}
