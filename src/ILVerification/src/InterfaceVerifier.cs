// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Resources;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    internal class InterfaceVerifier
    {
        private EcmaModule _module;
        private TypeDefinitionHandle _typeDefinitionHandle;
        private Lazy<ResourceManager> _stringResourceManager;
        public InterfaceVerifier(EcmaModule module, TypeDefinitionHandle typeDefinitionHandle, Lazy<ResourceManager> stringResourceManager)
        {
            _module = module;
            _typeDefinitionHandle = typeDefinitionHandle;
            _stringResourceManager = stringResourceManager;
        }

        public Action<InterfaceVerificationResult> InterfaceVerificationResult
        {
            set;
            private get;
        }

        public void Verify()
        {
            TypeDefinition typeDefinition = _module.MetadataReader.GetTypeDefinition(_typeDefinitionHandle);
            EcmaType type = (EcmaType)_module.GetType(_typeDefinitionHandle);
            // if not interface or abstract
            if (!type.IsInterface && !type.IsAbstract)
            {
                InterfaceImplementationHandleCollection interfaceHandles = typeDefinition.GetInterfaceImplementations();
                int count = interfaceHandles.Count;
                if (count == 0)
                {
                    return;
                }

                // Look for duplicates.               
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
                        InterfaceVerificationResult?.Invoke(new InterfaceVerificationResult()
                        {
                            Type = _typeDefinitionHandle,
                            Error = new InterfaceVerificationErrorArgs()
                            {
                                Code = VerifierError.InterfaceImplHasDuplicate,
                                TokenClass = _module.MetadataReader.GetToken(_typeDefinitionHandle),
                                TokenInterface = _module.MetadataReader.GetToken(interfaceHandle)
                            },
                            Message = string.Format(_stringResourceManager.Value.GetString(VerifierError.InterfaceImplHasDuplicate.ToString(), CultureInfo.InvariantCulture), interfaceType.ToString())
                        });
                    }
                }

                // Looks for missing method implementation
                foreach (InterfaceMetadataObjects interfaceMetadataObjects in implementedInterfaces.Distinct())
                {
                    foreach (MethodDesc method in interfaceMetadataObjects.DefType.GetAllMethods())
                    {
                        if (!IsImplemented(type, method))
                        {
                            InterfaceVerificationResult?.Invoke(new InterfaceVerificationResult()
                            {
                                Type = _typeDefinitionHandle,
                                Error = new InterfaceVerificationErrorArgs()
                                {
                                    Code = VerifierError.InterfaceMethodNotImplemented,
                                    TokenClass = _module.MetadataReader.GetToken(_typeDefinitionHandle),
                                    TokenInterface = _module.MetadataReader.GetToken(interfaceMetadataObjects.InterfaceImplementationHandle)
                                },
                                Message = string.Format(_stringResourceManager.Value.GetString(VerifierError.InterfaceMethodNotImplemented.ToString(), CultureInfo.InvariantCulture), interfaceMetadataObjects.DefType.ToString(), method.ToString())
                            });
                        }
                    }
                }
            }
        }

        // Search method matches name and sign recursively        
        private bool IsImplemented(EcmaType type, MethodDesc interfaceMethod)
        {
            MethodDesc result = null;
            TypeDesc currentType = type;
            do
            {
                result = FindMethod(type, interfaceMethod);
                currentType = currentType.BaseType;
            }
            while (result == null && currentType != null);

            return result != null;
        }

        private MethodDesc FindMethod(EcmaType currentType, MethodDesc interfaceMethod)
        {
            MetadataType interfaceType = (MetadataType)interfaceMethod.OwningType;

            string name = interfaceMethod.Name;
            MethodSignature sig = interfaceMethod.Signature;

            // Check among methods
            foreach (MethodDesc candidate in currentType.GetAllMethods())
            {
                if (candidate.Name == name)
                {
                    if (candidate.Signature.Equals(sig) && candidate.IsVirtual)
                    {
                        return candidate;
                    }
                }
            }
            
            return null;
        }
    }

    internal class InterfaceMetadataObjects : IEquatable<InterfaceMetadataObjects>
    {
        public DefType DefType { get; set; }
        public InterfaceImplementationHandle InterfaceImplementationHandle { get; set; }
        public bool Equals(InterfaceMetadataObjects other)
        {
            return other.DefType == DefType;
        }
    }
}
