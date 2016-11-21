// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using AssemblyName = System.Reflection.AssemblyName;
using Debug = System.Diagnostics.Debug;

namespace Internal.IL.Stubs.StartupCode
{
    /// <summary>
    /// Startup code that does initialization, Main invocation
    /// and shutdown of the runtime.
    /// </summary>
    public sealed class StartupCodeMainMethod : ILStubMethod
    {
        private TypeDesc _owningType;
        private MethodDesc _mainMethod;
        private MethodSignature _signature;

        public StartupCodeMainMethod(TypeDesc owningType, MethodDesc mainMethod)
        {
            _owningType = owningType;
            _mainMethod = mainMethod;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _owningType.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        public override string Name
        {
            get
            {
                return "StartupCodeMain";
            }
        }

        public override MethodIL EmitIL()
        {
            ILEmitter emitter = new ILEmitter();
            ILCodeStream codeStream = emitter.NewCodeStream();

            ModuleDesc developerExperience = Context.ResolveAssembly(new AssemblyName("System.Private.DeveloperExperience.Console"), false);
            if (developerExperience != null)
            {
                TypeDesc connectorType = developerExperience.GetKnownType("Internal.DeveloperExperience", "DeveloperExperienceConnectorConsole");
                MethodDesc initializeMethod = connectorType.GetKnownMethod("Initialize", null);
                codeStream.Emit(ILOpcode.call, emitter.NewToken(initializeMethod));
            }

            MetadataType startup = Context.GetHelperType("StartupCodeHelpers");

            // Initialize command line args if the class library supports this
            string initArgsName = (Context.Target.OperatingSystem == TargetOS.Windows)
                                ? "InitializeCommandLineArgsW"
                                : "InitializeCommandLineArgs";
            MethodDesc initArgs = startup.GetMethod(initArgsName, null);
            if (initArgs != null)
            {
                codeStream.Emit(ILOpcode.ldarg_0); // argc
                codeStream.Emit(ILOpcode.ldarg_1); // argv
                codeStream.Emit(ILOpcode.call, emitter.NewToken(initArgs));
            }

            // Call program Main
            if (_mainMethod.Signature.Length > 0)
            {
                // TODO: better exception
                if (initArgs == null)
                    throw new Exception("Main() has parameters, but the class library doesn't support them");

                codeStream.Emit(ILOpcode.call, emitter.NewToken(startup.GetKnownMethod("GetMainMethodArguments", null)));
            }
            codeStream.Emit(ILOpcode.call, emitter.NewToken(_mainMethod));

            MethodDesc setLatchedExitCode = startup.GetMethod("SetLatchedExitCode", null);
            MethodDesc shutdown = startup.GetMethod("Shutdown", null);

            // The class library either supports "advanced shutdown", or doesn't. No half-implementations allowed.
            Debug.Assert((setLatchedExitCode != null) == (shutdown != null));

            if (setLatchedExitCode != null)
            {
                // If the main method has a return value, save it
                if (!_mainMethod.Signature.ReturnType.IsVoid)
                {
                    codeStream.Emit(ILOpcode.call, emitter.NewToken(setLatchedExitCode));
                }

                // Ask the class library to shut down and return exit code.
                codeStream.Emit(ILOpcode.call, emitter.NewToken(shutdown));
            }
            else
            {
                // This is a class library that doesn't have SetLatchedExitCode/Shutdown.
                // If the main method returns void, we simply use 0 exit code.
                if (_mainMethod.Signature.ReturnType.IsVoid)
                {
                    codeStream.EmitLdc(0);
                }
            }

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    _signature = new MethodSignature(MethodSignatureFlags.Static, 0,
                            Context.GetWellKnownType(WellKnownType.Int32),
                            new TypeDesc[2] {
                                Context.GetWellKnownType(WellKnownType.Int32),
                                Context.GetWellKnownType(WellKnownType.IntPtr) });
                }

                return _signature;
            }
        }

        public override bool IsNativeCallable
        {
            get
            {
                return true;
            }
        }
    }
}
