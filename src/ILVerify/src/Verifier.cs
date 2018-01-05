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
using System.Text;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    public class Verifier
    {
        private Lazy<ResourceManager> _stringResourceManager =
            new Lazy<ResourceManager>(() => new ResourceManager("ILVerify.Resources.Strings", Assembly.GetExecutingAssembly()));

        private SimpleTypeSystemContext _typeSystemContext;

        private static VerificationResult s_noSystemModuleResult = new VerificationResult() { Message = "No system module specified" };

        public Verifier(IResolver resolver)
        {
            _typeSystemContext = new SimpleTypeSystemContext(resolver);
        }

        internal Verifier(SimpleTypeSystemContext context)
        {
            _typeSystemContext = context;
        }

        public void SetSystemModuleName(AssemblyName name)
        {
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModule(_typeSystemContext._resolver.Resolve(name)));
        }

        public IEnumerable<VerificationResult> Verify(PEReader peReader)
        {
            if (peReader == null)
            {
                throw new ArgumentNullException(nameof(peReader));
            }

            if (_typeSystemContext.SystemModule is null)
            {
                yield return s_noSystemModuleResult;
                yield break;
            }

            IEnumerable<VerificationResult> results;
            try
            {
                EcmaModule module = _typeSystemContext.GetModule(peReader);
                results = VerifyMethods(module, peReader.GetSimpleName(), module.MetadataReader.MethodDefinitions);
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
            if (peReader is null)
            {
                throw new ArgumentNullException(nameof(peReader));
            }

            if (typeHandle.IsNil)
            {
                throw new ArgumentNullException(nameof(typeHandle));
            }

            if (_typeSystemContext.SystemModule is null)
            {
                yield return s_noSystemModuleResult;
                yield break;
            }

            IEnumerable<VerificationResult> results;
            try
            {
                EcmaModule module = _typeSystemContext.GetModule(peReader);
                var typeDef = peReader.GetMetadataReader().GetTypeDefinition(typeHandle);
                results = VerifyMethods(module, peReader.GetSimpleName(), typeDef.GetMethods());
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
            if (peReader is null)
            {
                throw new ArgumentNullException(nameof(peReader));
            }

            if (methodHandle.IsNil)
            {
                throw new ArgumentNullException(nameof(methodHandle));
            }

            if (_typeSystemContext.SystemModule is null)
            {
                yield return s_noSystemModuleResult;
                yield break;
            }

            IEnumerable<VerificationResult> results;
            try
            {
                EcmaModule module = _typeSystemContext.GetModule(peReader);
                results = VerifyMethods(module, peReader.GetSimpleName(), new[] { methodHandle });
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

        private IEnumerable<VerificationResult> VerifyMethods(EcmaModule module, string moduleName, IEnumerable<MethodDefinitionHandle> methodHandles)
        {
            foreach (var methodHandle in methodHandles)
            {
                var method = (EcmaMethod)module.GetMethod(methodHandle);
                var methodIL = EcmaMethodIL.Create(method);

                if (methodIL != null)
                {
                    var results = VerifyMethod(module, moduleName, method, methodIL);
                    foreach (var result in results)
                    {
                        yield return result;
                    }
                }
            }
        }

        private IEnumerable<VerificationResult> VerifyMethod(EcmaModule module, string moduleName, MethodDesc method, MethodIL methodIL)
        {
            var builder = new ArrayBuilder<VerificationResult>();

            try
            {
                var importer = new ILImporter(method, methodIL);

                importer.ReportVerificationError = (args) =>
                {
                    var codeResource = _stringResourceManager.Value.GetString(args.Code.ToString(), CultureInfo.InvariantCulture);

                    builder.Add(new VerificationResult()
                    {
                        ModuleName = moduleName,
                        TypeName = ((EcmaType)method.OwningType).Name,
                        Method = MethodDescription(method),
                        Error = args,
                        Message = string.IsNullOrEmpty(codeResource) ? args.Code.ToString() : codeResource
                    });
                };

                importer.Verify();
            }
            catch (NotImplementedException e)
            {
                reportException(e);
            }
            catch (InvalidProgramException e)
            {
                reportException(e);
            }
            catch (VerificationException)
            {
                // a result was reported already (before aborting)
            }
            catch (BadImageFormatException)
            {
                builder.Add(new VerificationResult()
                {
                    ModuleName = moduleName,
                    TypeName = ((EcmaType)method.OwningType).Name,
                    Method = method.Name,
                    Message = "Unable to resolve token"
                });
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
                    ModuleName = moduleName,
                    TypeName = ((EcmaType)method.OwningType).Name,
                    Method = method.Name,
                    Message = e.Message
                });
            }
        }

        private string MethodDescription(MethodDesc method)
        {
            StringBuilder description = new StringBuilder();
            description.Append(method.Name);
            description.Append("(");

            if (method.Signature._parameters != null && method.Signature._parameters.Length > 0)
            {
                foreach (TypeDesc parameter in method.Signature._parameters)
                {
                    description.Append(parameter.ToString());
                    description.Append(", ");
                }
                description.Remove(description.Length - 2, 2);
            }

            description.Append(")");
            return description.ToString();
        }
    }
}
