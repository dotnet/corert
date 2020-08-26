// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Internal.TypeSystem;

namespace Internal.IL.Stubs.StartupCode
{
    public sealed partial class AppContextInitializerMethod : ILStubMethod
    {
        private TypeDesc _owningType;
        private MethodSignature _signature;
        private IReadOnlyCollection<string> _switches;

        public AppContextInitializerMethod(TypeDesc owningType, IEnumerable<string> switches)
        {
            _owningType = owningType;
            _switches = new List<string>(switches);
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
                return "SetAppContextSwitches";
            }
        }

        public override MethodIL EmitIL()
        {
            ILEmitter emitter = new ILEmitter();
            ILCodeStream codeStream = emitter.NewCodeStream();

            MetadataType appContextType = Context.SystemModule.GetKnownType("System", "AppContext");
            MethodDesc setSwitchMethod = appContextType.GetKnownMethod("SetSwitch", null);
            ILToken setSwitchToken = emitter.NewToken(setSwitchMethod);

            foreach (string switchName in _switches)
            {
                codeStream.Emit(ILOpcode.ldstr, emitter.NewToken(switchName));
                codeStream.EmitLdc(1);
                codeStream.Emit(ILOpcode.call, setSwitchToken);
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
                            Context.GetWellKnownType(WellKnownType.Void),
                            TypeDesc.EmptyTypes);
                }

                return _signature;
            }
        }
    }
}
