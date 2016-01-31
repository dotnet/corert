using Internal.TypeSystem;
using Internal.IL;
using Internal.IL.Stubs;
using System;

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
                return OwningType.Context;
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
            ILLocalVariable returnValue = emitter.NewLocal(Context.GetWellKnownType(WellKnownType.Int32));

            TypeDesc startup = Context.SystemModule.GetType("Internal.Runtime.CompilerHelpers", "StartupCodeHelpers");
            
            codeStream.Emit(ILOpcode.call, emitter.NewToken(startup.GetMethod("Initialize", null)));

            // Initialize command line args
            string initArgsName = (Context.Target.OperatingSystem == TargetOS.Windows)
                                ? "InitializeCommandLineArgsW"
                                : "InitializeCommandLineArgs";
            MethodDesc initArgs = startup.GetMethod(initArgsName, null);
            codeStream.Emit(ILOpcode.ldarg_0); // argc
            codeStream.Emit(ILOpcode.ldarg_1); // argv
            codeStream.Emit(ILOpcode.call, emitter.NewToken(initArgs));

            // Call program Main
            if (_mainMethod.Signature.Length > 0)
            {
                TypeDesc environ = Context.SystemModule.GetType("System", "Environment");
                codeStream.Emit(ILOpcode.call, emitter.NewToken(environ.GetMethod("GetCommandLineArgs", null)));
            }
            codeStream.Emit(ILOpcode.call, emitter.NewToken(_mainMethod));
            if (_mainMethod.Signature.ReturnType.IsVoid)
            {
                codeStream.EmitLdc(0);
            }
            codeStream.EmitStLoc(returnValue);

            codeStream.Emit(ILOpcode.call, emitter.NewToken(startup.GetMethod("Shutdown", null)));

            codeStream.EmitLdLoc(returnValue);
            codeStream.Emit(ILOpcode.ret);

            return emitter.Link();
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
    }
}

