// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ILVerify;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.TypeVerifier
{
    internal class TypeVerifier
    {
        private readonly EcmaModule _module;
        private readonly TypeDefinitionHandle _typeDefinitionHandle;
        private readonly  ILVerifyTypeSystemContext _typeSystemContext;
        public Action<VerifierError, object[]> ReportVerificationError
        {
            set;
            private get;
        }

        private void VerificationError(VerifierError error, params object[] args)
        {
            ReportVerificationError(error, args);
        }

        public TypeVerifier(EcmaModule module, TypeDefinitionHandle typeDefinitionHandle, ILVerifyTypeSystemContext typeSystemContext)
        {
            _module = module;
            _typeDefinitionHandle = typeDefinitionHandle;
            _typeSystemContext = typeSystemContext;
        }

        public void Verify()
        {
            VerifyInterfaces();
        }

        public void VerifyInterfaces()
        {
            TypeDefinition typeDefinition = _module.MetadataReader.GetTypeDefinition(_typeDefinitionHandle);
            EcmaType type = (EcmaType)_module.GetType(_typeDefinitionHandle);

            if (type.IsInterface)
            {
                return;
            }

            InterfaceImplementationHandleCollection interfaceHandles = typeDefinition.GetInterfaceImplementations();
            int count = interfaceHandles.Count;
            if (count == 0)
            {
                return;
            }

            VirtualMethodAlgorithm virtualMethodAlg = _typeSystemContext.GetVirtualMethodAlgorithmForType(type);
            List<InterfaceMetadataObjects> implementedInterfaces = new List<InterfaceMetadataObjects>();
            foreach (InterfaceImplementationHandle interfaceHandle in interfaceHandles)
            {
                InterfaceImplementation interfaceImplementation = _module.MetadataReader.GetInterfaceImplementation(interfaceHandle);
                DefType interfaceType = _module.GetType(interfaceImplementation.Interface) as DefType;
                if (interfaceType == null)
                {
                    ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadBadFormat, type);
                }

                InterfaceMetadataObjects imo = new InterfaceMetadataObjects
                {
                    DefType = interfaceType,
                    InterfaceImplementationHandle = interfaceHandle
                };

                // Look for duplicates.
                if (!implementedInterfaces.Contains(imo))
                {
                    implementedInterfaces.Add(imo);
                }
                else
                {
                    VerificationError(VerifierError.InterfaceImplHasDuplicate, type, interfaceType, _module.MetadataReader.GetToken(interfaceHandle));
                }

                if (!type.IsAbstract)
                { 
                    // Look for missing method implementation
                    foreach (MethodDesc method in imo.DefType.GetAllMethods())
                    {
                        MethodDesc resolvedMethod = virtualMethodAlg.ResolveInterfaceMethodToVirtualMethodOnType(method, type);
                        if (resolvedMethod is null)
                        {
                            VerificationError(VerifierError.InterfaceMethodNotImplemented, type, imo.DefType, method, _module.MetadataReader.GetToken(_typeDefinitionHandle), _module.MetadataReader.GetToken(imo.InterfaceImplementationHandle), _module.MetadataReader.GetToken(((EcmaMethod)method).Handle));
                        }
                    }
                }
            }
        }

        private class InterfaceMetadataObjects : IEquatable<InterfaceMetadataObjects>
        {
            public DefType DefType { get; set; }
            public InterfaceImplementationHandle InterfaceImplementationHandle { get; set; }
            public bool Equals(InterfaceMetadataObjects other)
            {
                return other.DefType == DefType;
            }
        }
    }
}
