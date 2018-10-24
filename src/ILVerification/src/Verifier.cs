// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    public class Verifier
    {
        private Lazy<ResourceManager> _stringResourceManager =
            new Lazy<ResourceManager>(() => new ResourceManager("FxResources.ILVerification.SR", typeof(Verifier).GetTypeInfo().Assembly));

        private ILVerifyTypeSystemContext _typeSystemContext;

        public Verifier(IResolver resolver)
        {
            _typeSystemContext = new ILVerifyTypeSystemContext(resolver);
        }

        internal Verifier(ILVerifyTypeSystemContext context)
        {
            _typeSystemContext = context;
        }

        public void SetSystemModuleName(AssemblyName name)
        {
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModule(_typeSystemContext._resolver.Resolve(name.Name)));
        }

        internal EcmaModule GetModule(PEReader peReader)
        {
            return _typeSystemContext.GetModule(peReader);
        }

        public IEnumerable<VerificationResult> Verify(PEReader peReader)
        {
            if (peReader == null)
            {
                throw new ArgumentNullException(nameof(peReader));
            }

            if (_typeSystemContext.SystemModule == null)
            {
                ThrowMissingSystemModule();
            }

            IEnumerable<VerificationResult> results;
            try
            {
                EcmaModule module = GetModule(peReader);
                results = VerifyMethods(module, module.MetadataReader.MethodDefinitions);
            }
            catch (VerifierException e)
            {
                results = new[] { new VerificationResult() { Message = e.Message } };
            }

            foreach (var result in results)
            {
                yield return result;
            }
        }

        public IEnumerable<VerificationResult> Verify(PEReader peReader, TypeDefinitionHandle typeHandle, bool verifyMethods = false)
        {
            if (peReader == null)
            {
                throw new ArgumentNullException(nameof(peReader));
            }

            if (typeHandle.IsNil)
            {
                throw new ArgumentNullException(nameof(typeHandle));
            }

            if (_typeSystemContext.SystemModule == null)
            {
                ThrowMissingSystemModule();
            }

            IEnumerable<VerificationResult> results;
            try
            {
                EcmaModule module = GetModule(peReader);
                MetadataReader metadataReader = peReader.GetMetadataReader();
                
                results = VerifyInterface(module, typeHandle);

                if (verifyMethods)
                {
                    TypeDefinition typeDef = metadataReader.GetTypeDefinition(typeHandle);
                    results = results.Union(VerifyMethods(module, typeDef.GetMethods()));
                }
            }
            catch (VerifierException e)
            {
                results = new[] { new VerificationResult() { Message = e.Message } };
            }

            foreach (var result in results)
            {
                yield return result;
            }
        }

        public IEnumerable<VerificationResult> Verify(PEReader peReader, MethodDefinitionHandle methodHandle)
        {
            if (peReader == null)
            {
                throw new ArgumentNullException(nameof(peReader));
            }

            if (methodHandle.IsNil)
            {
                throw new ArgumentNullException(nameof(methodHandle));
            }

            if (_typeSystemContext.SystemModule == null)
            {
                ThrowMissingSystemModule();
            }

            IEnumerable<VerificationResult> results;
            try
            {
                EcmaModule module = GetModule(peReader);
                results = VerifyMethods(module, new[] { methodHandle });
            }
            catch (VerifierException e)
            {
                results = new[] { new VerificationResult() { Message = e.Message } };
            }

            foreach (var result in results)
            {
                yield return result;
            }
        }

        private IEnumerable<VerificationResult> VerifyMethods(EcmaModule module, IEnumerable<MethodDefinitionHandle> methodHandles)
        {
            foreach (var methodHandle in methodHandles)
            {
                var method = (EcmaMethod)module.GetMethod(methodHandle);
                var methodIL = EcmaMethodIL.Create(method);

                if (methodIL != null)
                {
                    var results = VerifyMethod(module, methodIL, methodHandle);
                    foreach (var result in results)
                    {
                        yield return result;
                    }
                }
            }
        }

        private IEnumerable<VerificationResult> VerifyMethod(EcmaModule module, MethodIL methodIL, MethodDefinitionHandle methodHandle)
        {
            var builder = new ArrayBuilder<VerificationResult>();
            MethodDesc method = methodIL.OwningMethod; 

            try
            {
                var importer = new ILImporter(method, methodIL);

                importer.ReportVerificationError = (args) =>
                {
                    var codeResource = _stringResourceManager.Value.GetString(args.Code.ToString(), CultureInfo.InvariantCulture);

                    builder.Add(new VerificationResult()
                    {
                        Method = methodHandle,
                        Error = args,
                        Message = string.IsNullOrEmpty(codeResource) ? args.Code.ToString() : codeResource
                    });
                };

                importer.Verify();
            }
            catch (VerificationException)
            {
                // a result was reported already (before aborting)
            }
            catch (BadImageFormatException)
            {
                builder.Add(new VerificationResult()
                {
                    Method = methodHandle,
                    Message = "Unable to resolve token"
                });
            }
            catch (NotImplementedException e)
            {
                reportException(e);
            }
            catch (InvalidProgramException e)
            {
                reportException(e);
            }
            catch (PlatformNotSupportedException e)
            {
                reportException(e);
            }
            catch (VerifierException e)
            {
                reportException(e);
            }
            catch (TypeSystemException e)
            {
                reportException(e);
            }

            return builder.ToArray();

            void reportException(Exception e)
            {
                builder.Add(new VerificationResult()
                {
                    Method = methodHandle,
                    Message = e.Message
                });
            }
        }

        private IEnumerable<VerificationResult> VerifyInterface(EcmaModule module, TypeDefinitionHandle typeDefinitionHandle)
        {
            var builder = new ArrayBuilder<VerificationResult>();
            
            try
            {
                TypeDefinition typeDefinition = module.MetadataReader.GetTypeDefinition(typeDefinitionHandle);
                EcmaType type = (EcmaType)module.GetType(typeDefinitionHandle);

                // if not interface or abstract
                if (!type.IsInterface && !type.IsAbstract)
                {
                    // Look for duplicates.
                    foreach (var interfaceImplemented in type.ExplicitlyImplementedInterfaces.GroupBy(i => i))
                    {
                        if(interfaceImplemented.Count() > 1)
                        {
                            builder.Add(new VerificationResult()
                            {
                                Type = typeDefinitionHandle,
                                Error = new VerificationErrorArgs() { Code = VerifierError.InterfaceImplHasDuplicate },
                                Message = string.Format(_stringResourceManager.Value.GetString(VerifierError.InterfaceImplHasDuplicate.ToString(), CultureInfo.InvariantCulture), interfaceImplemented.Key.ToString())
                            });
                        }
                    }

                    foreach (DefType interfaceImplemented in type.ExplicitlyImplementedInterfaces.Distinct())
                    {
                        foreach (MethodDesc method in interfaceImplemented.GetAllMethods())
                        {
                            if(type.ResolveInterfaceMethodTarget(method) == null)
                            {
                                builder.Add(new VerificationResult()
                                {
                                    Type = typeDefinitionHandle,
                                    Error = new VerificationErrorArgs() { Code = VerifierError.InterfaceMethodNotImplemented },
                                    Message = string.Format(_stringResourceManager.Value.GetString(VerifierError.InterfaceMethodNotImplemented.ToString(), CultureInfo.InvariantCulture), interfaceImplemented.ToString(), method.ToString())
                                });
                            }
                        }
                    }
                }
            }
            catch (BadImageFormatException)
            {
                builder.Add(new VerificationResult()
                {
                    Type = typeDefinitionHandle,
                    Message = "Unable to resolve token"
                });
            }
            catch (NotImplementedException e)
            {
                reportException(e);
            }
            catch (InvalidProgramException e)
            {
                reportException(e);
            }
            catch (PlatformNotSupportedException e)
            {
                reportException(e);
            }
            catch (VerifierException e)
            {
                reportException(e);
            }
            catch (TypeSystemException e)
            {
                reportException(e);
            }

            return builder.ToArray();

            void reportException(Exception e)
            {
                builder.Add(new VerificationResult()
                {
                    Type = typeDefinitionHandle,
                    Message = e.Message
                });
            }
        }

        private void ThrowMissingSystemModule()
        {
            throw new VerifierException("No system module specified");
        }
    }
}
