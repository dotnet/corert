// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using ILVerify;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.TypeVerifier
{
    internal class TypeVerifier
    {
        private readonly EcmaModule _module;
        private readonly TypeDefinitionHandle _typeDefinitionHandle;
        private readonly ILVerifyTypeSystemContext _typeSystemContext;
        private readonly VerifierOptions _verifierOptions;

        public Action<VerifierError, object[]> ReportVerificationError
        {
            set;
            private get;
        }

        private void VerificationError(VerifierError error, params object[] args)
        {
            ReportVerificationError(error, args);
        }

        public TypeVerifier(EcmaModule module, TypeDefinitionHandle typeDefinitionHandle, ILVerifyTypeSystemContext typeSystemContext, VerifierOptions verifierOptions)
        {
            _module = module;
            _typeDefinitionHandle = typeDefinitionHandle;
            _typeSystemContext = typeSystemContext;
            _verifierOptions = verifierOptions;
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

            // Look for duplicates and prepare distinct list of implemented interfaces to avoid 
            // subsequent error duplication
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

                if (!implementedInterfaces.Contains(imo))
                {
                    implementedInterfaces.Add(imo);
                }
                else
                {
                    VerificationError(VerifierError.InterfaceImplHasDuplicate, Format(type), Format(interfaceType));
                }
            }

            foreach (InterfaceMetadataObjects implementedInterface in implementedInterfaces)
            {
                if (!type.IsAbstract)
                {
                    // Look for missing method implementation
                    foreach (MethodDesc method in implementedInterface.DefType.GetAllMethods())
                    {
                        MethodDesc resolvedMethod = type.ResolveInterfaceMethodTarget(method);
                        if (resolvedMethod is null)
                        {
                            VerificationError(VerifierError.InterfaceMethodNotImplemented, Format(type), Format(implementedInterface.DefType), Format(method));
                        }
                    }
                }
            }
        }

        // Format helpers for future use
        // The idea is to improve formatting string with tokens value i.e.: 
        // [Assembly]Class(0x1234) vs current [Assembly]Class
        // in case somebody will pass '--tokens' switch to ILVerify
        private string Format(EcmaType type)
        {
            return type.ToString();
        }

        private string Format(DefType interfaceTypeDef)
        {
            return interfaceTypeDef.ToString();
        }

        private string Format(MethodDesc methodDesc)
        {
            return methodDesc.ToString();
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
