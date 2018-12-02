using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Resources;
using ILVerify;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.TypeVerifier
{
    internal class TypeVerifier
    {
        private EcmaModule _module;
        private readonly TypeDefinitionHandle _typeDefinitionHandle;
        private readonly Lazy<ResourceManager> _stringResourceManager;

        public Action<ErrorArgument[], VerifierError, string> ReportVerificationError
        {
            set;
            private get;
        }

        public TypeVerifier(EcmaModule module, TypeDefinitionHandle typeDefinitionHandle, Lazy<ResourceManager> stringResourceManager)
        {
            _module = module;
            _typeDefinitionHandle = typeDefinitionHandle;
            _stringResourceManager = stringResourceManager;
        }

        public void Verify()
        {
            VerifyInterfaces();
        }

        public void VerifyInterfaces()
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
                        var args = new ErrorArgument[]
                        {
                            new ErrorArgument("TokenClass", _module.MetadataReader.GetToken(_typeDefinitionHandle)),
                            new ErrorArgument("TokenInterface", _module.MetadataReader.GetToken(interfaceHandle))
                        };
                        string message = _stringResourceManager.Value.GetString(VerifierError.InterfaceImplHasDuplicate.ToString(), CultureInfo.InvariantCulture);
                        ReportVerificationError(args, VerifierError.InterfaceImplHasDuplicate, string.Format(message, type.ToString(), interfaceType.ToString()));
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
