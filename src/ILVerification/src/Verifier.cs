// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
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
            PEReader peReader = _typeSystemContext._resolver.Resolve(name.Name);
            if (peReader is null)
            {
                throw new VerifierException("Assembly or module not found: " + name.FullName);
            }
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModule(peReader));
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

        public IEnumerable<VerificationResult> Verify(PEReader peReader, TypeDefinitionHandle typeHandle)
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
                TypeDefinition typeDef = peReader.GetMetadataReader().GetTypeDefinition(typeHandle);
                results = VerifyMethods(module, typeDef.GetMethods());
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

        private void ThrowMissingSystemModule()
        {
            throw new VerifierException("No system module specified");
        }
    }
}
