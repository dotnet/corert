// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                    InterfaceImplementation = interfaceImplementation,
                    InterfaceImplementationHandle = interfaceHandle
                };

                if (!implementedInterfaces.Contains(imo))
                {
                    implementedInterfaces.Add(imo);
                }
                else
                {
                    VerificationError(VerifierError.InterfaceImplHasDuplicate, Format(type), Format(imo.DefType, imo.InterfaceImplementation));
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
                            VerificationError(VerifierError.InterfaceMethodNotImplemented, Format(type), Format(implementedInterface.DefType, implementedInterface.InterfaceImplementation), Format(method));
                        }
                    }
                }
            }
        }

        private string Format(EcmaType type)
        {
            if (_verifierOptions.IncludeMetadataTokensInErrorMessages)
            {
                return string.Format("{0}([{1}]0x{2:X8})", type, type.Module, _module.MetadataReader.GetToken(type.Handle));
            }
            else
            {
                return type.ToString();
            }
        }

        private string Format(DefType defType, InterfaceImplementation interfaceImplementation)
        {
            if (_verifierOptions.IncludeMetadataTokensInErrorMessages)
            {

                return string.Format("{0}([{1}]0x{2:X8})", defType, GetModule(defType), _module.MetadataReader.GetToken(interfaceImplementation.Interface));
            }
            else
            {
                return defType.ToString();
            }
        }

        private string GetModule(DefType defType)
        {
            if (defType is MetadataType metadataType)
            {
                return metadataType.Module.ToString();
            }

            Debug.Fail("ModuleNotFound");
            return "ModuleNotFound";
        }

        private string Format(MethodDesc methodDesc)
        {
            if (_verifierOptions.IncludeMetadataTokensInErrorMessages)
            {
                return string.Format("{0}([{1}]0x{2:X8})", methodDesc, GetModule(methodDesc), _module.MetadataReader.GetToken(((EcmaMethod)methodDesc.GetTypicalMethodDefinition()).Handle));
            }
            else
            {
                return methodDesc.ToString();
            }
        }

        private string GetModule(MethodDesc methodDesc)
        {
            if (methodDesc.GetMethodDefinition().OwningType is MetadataType metadataType)
            {
                return metadataType.Module.ToString();
            }

            Debug.Fail("ModuleNotFound");
            return "ModuleNotFound";
        }

        private class InterfaceMetadataObjects : IEquatable<InterfaceMetadataObjects>
        {
            public DefType DefType { get; set; }
            public InterfaceImplementation InterfaceImplementation { set; get; }
            public InterfaceImplementationHandle InterfaceImplementationHandle { get; set; }
            public bool Equals(InterfaceMetadataObjects other)
            {
                return other.DefType == DefType;
            }
        }
    }
}
