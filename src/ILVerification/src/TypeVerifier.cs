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
                    InterfaceDefType = interfaceType,
                    InterfaceImplementation = interfaceImplementation
                };

                if (!implementedInterfaces.Contains(imo))
                {
                    implementedInterfaces.Add(imo);
                }
                else
                {
                    VerificationError(VerifierError.InterfaceImplHasDuplicate, Format(type), Format(_module, imo.InterfaceDefType, imo.InterfaceImplementation));
                }
            }

            foreach (InterfaceMetadataObjects implementedInterface in implementedInterfaces)
            {
                if (!type.IsAbstract)
                {
                    // Look for missing method implementation
                    foreach (MethodDesc method in implementedInterface.InterfaceDefType.GetAllMethods())
                    {
                        MethodDesc resolvedMethod = type.ResolveInterfaceMethodTarget(method);
                        if (resolvedMethod is null)
                        {
                            VerificationError(VerifierError.InterfaceMethodNotImplemented, Format(type), Format(_module, implementedInterface.InterfaceDefType, implementedInterface.InterfaceImplementation), Format(method));
                        }
                    }
                }
            }
        }

        private string Format(EcmaType type)
        {
            if (_verifierOptions.IncludeMetadataTokensInErrorMessages)
            {
                TypeDesc typeDesc = type.GetTypeDefinition();
                EcmaModule module = (EcmaModule)((MetadataType)typeDesc).Module;

                return string.Format("{0}([{1}]0x{2:X8})", type, module, module.MetadataReader.GetToken(_typeDefinitionHandle));
            }
            else
            {
                return type.ToString();
            }
        }

        private string Format(EcmaModule module, DefType interfaceDefType, InterfaceImplementation interfaceImplementation)
        {
            if (_verifierOptions.IncludeMetadataTokensInErrorMessages)
            {
                return string.Format("{0}([{1}]0x{2:X8})", interfaceDefType, module, module.MetadataReader.GetToken(interfaceImplementation.Interface));
            }
            else
            {
                return interfaceDefType.ToString();
            }
        }

        private string Format(MethodDesc methodDesc)
        {
            if (_verifierOptions.IncludeMetadataTokensInErrorMessages)
            {
                TypeDesc typeDesc = methodDesc.OwningType.GetTypeDefinition();
                EcmaModule module = (EcmaModule)((MetadataType)typeDesc).Module;

                return string.Format("{0}([{1}]0x{2:X8})", methodDesc, module, module.MetadataReader.GetToken(((EcmaMethod)methodDesc.GetTypicalMethodDefinition()).Handle));
            }
            else
            {
                return methodDesc.ToString();
            }
        }

        private class InterfaceMetadataObjects : IEquatable<InterfaceMetadataObjects>
        {
            public DefType InterfaceDefType { get; set; }
            public InterfaceImplementation InterfaceImplementation { get; set; }
            public bool Equals(InterfaceMetadataObjects other)
            {
                return other.InterfaceDefType == InterfaceDefType;
            }
        }
    }
}
