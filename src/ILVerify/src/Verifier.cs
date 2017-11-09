// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Text;
using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILVerify
{
    public delegate bool ShouldVerifyMethod(string name);

    public interface IResolver
    {
        PEReader Resolve(AssemblyName name);
    }

    public class Verifier
    {
        private Lazy<ResourceManager> _stringResourceManager =
            new Lazy<ResourceManager>(() => new ResourceManager("ILVerify.Resources.Strings", Assembly.GetExecutingAssembly()));

        private SimpleTypeSystemContext _typeSystemContext;

        public ShouldVerifyMethod ShouldVerifyMethod { private get; set; }

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
            _typeSystemContext.SetSystemModule(_typeSystemContext.GetModule(name));
        }

        public VerificationResult Verify(AssemblyName moduleToVerify)
        {
            if (moduleToVerify == null)
            {
                throw new ArgumentNullException(nameof(moduleToVerify));
            }

            try
            {
                if (_typeSystemContext.SystemModule is null)
                {
                    if (_typeSystemContext._inferredSystemModule != null)
                    {
                        _typeSystemContext.SetSystemModule(_typeSystemContext._inferredSystemModule);
                    }
                    else
                    {
                        throw new VerifierException("No system module specified");
                    }
                }

                EcmaModule module = _typeSystemContext.GetModule(moduleToVerify);
                return VerifyModule(module, moduleToVerify.Name);
            }
            catch (VerifierException e)
            {
                return new VerificationResult() { NumErrors = 1, Message = e.Message };
            }
        }

        private VerificationResult VerifyModule(EcmaModule module, string path)
        {
            foreach (var methodHandle in module.MetadataReader.MethodDefinitions)
            {
                var method = (EcmaMethod)module.GetMethod(methodHandle);

                var methodIL = EcmaMethodIL.Create(method);
                if (methodIL == null)
                {
                    continue;
                }

                var methodName = method.ToString();
                if (ShouldVerifyMethod != null && !ShouldVerifyMethod(methodName))
                {
                    continue;
                }

                var result = VerifyMethod(method, methodIL, path);
                if (result.NumErrors > 0)
                {
                    return result;
                }
            }

            return new VerificationResult();
        }

        internal VerificationResult VerifyMethod(MethodDesc method, MethodIL methodIL, string moduleName)
        {
            StringBuilder output = new StringBuilder();
            int numErrors = 0;
            var errors = new List<VerifierError>();

            try
            {
                var importer = new ILImporter(method, methodIL);

                importer.ReportVerificationError = (args) =>
                {
                    AppendError(method, moduleName, args, output);
                    errors.Add(args.Code);
                    numErrors++;
                };

                importer.Verify();
            }
            catch (NotImplementedException e)
            {
                output.AppendLine($"Error in {method}: {e.Message}");
                numErrors++;
            }
            catch (InvalidProgramException e)
            {
                output.AppendLine($"Error in {method}: {e.Message}");
                numErrors++;
            }
            catch (VerificationException)
            {
                numErrors++;
            }
            catch (BadImageFormatException)
            {
                output.AppendLine("Unable to resolve token");
                numErrors++;
            }
            catch (PlatformNotSupportedException e)
            {
                output.AppendLine(e.Message);
                numErrors++;
            }
            catch (VerifierException e)
            {
                output.AppendLine(e.Message);
                numErrors++;
            }
            catch (TypeSystemException e)
            {
                output.AppendLine(e.Message);
                numErrors++;
            }

            return new VerificationResult() { NumErrors = numErrors, Message = output.ToString(), _errors = errors };
        }

        internal void AppendError(MethodDesc method, string moduleName, VerificationErrorArgs args, StringBuilder output)
        {
            output.Append("[IL]: Error: ");

            output.Append("[");
            output.Append(moduleName);
            output.Append(" : ");
            output.Append(((EcmaType)method.OwningType).Name);
            output.Append("::");
            output.Append(method.Name);
            output.Append("(");
            if (method.Signature._parameters != null && method.Signature._parameters.Length > 0)
            {
                foreach (TypeDesc parameter in method.Signature._parameters)
                {
                    output.Append(parameter.ToString());
                    output.Append(", ");
                }
                output.Remove(output.Length - 2, 2);
            }
            output.Append(")");
            output.Append("]");

            output.Append("[offset 0x");
            output.Append(args.Offset.ToString("X8"));
            output.Append("]");

            if (args.Found != null)
            {
                output.Append("[found ");
                output.Append(args.Found);
                output.Append("]");
            }

            if (args.Expected != null)
            {
                output.Append("[expected ");
                output.Append(args.Expected);
                output.Append("]");
            }

            if (args.Token != 0)
            {
                output.Append("[token  0x");
                output.Append(args.Token.ToString("X8"));
                output.Append("]");
            }

            output.Append(" ");
            var str = _stringResourceManager.Value.GetString(args.Code.ToString(), CultureInfo.InvariantCulture);
            output.AppendLine(string.IsNullOrEmpty(str) ? args.Code.ToString() : str);
        }
    }

    public class VerificationResult
    {
        public int NumErrors = 0;
        public string Message = string.Empty;
        internal IEnumerable<VerifierError> _errors; // Note: there may be fewer errors recorded here than counted in NumErrors, which also counts exceptions
    }

    public class VerifierException : Exception
    {
        public VerifierException(string message) : base(message)
        {
        }
    }
}
