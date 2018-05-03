// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Interop;
using Debug = System.Diagnostics.Debug;
using Internal.TypeSystem.Ecma;

namespace Internal.IL.Stubs
{
    /// <summary>
    /// Thunk to marshal delegate parameters and invoke the appropriate delegate function pointer
    /// </summary>
    public partial class CalliMarshallingMethodThunk : ILStubMethod
    {
        private readonly MethodSignature _targetSignature;
        private readonly InteropStateManager _interopStateManager;
        private readonly TypeDesc _owningType;

        private MethodSignature _signature;

        public CalliMarshallingMethodThunk(MethodSignature targetSignature, TypeDesc owningType,
                InteropStateManager interopStateManager)
        {
            _targetSignature = targetSignature;
            _owningType = owningType;
            _interopStateManager = interopStateManager;
        }

        public MethodSignature TargetSignature
        {
            get
            {
                return _targetSignature;
            }
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

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    // Prepend fnptr argument to the signature
                    TypeDesc[] parameterTypes = new TypeDesc[_targetSignature.Length + 1];

                    parameterTypes[0] = Context.GetWellKnownType(WellKnownType.IntPtr);
                    for (int i = 0; i < _targetSignature.Length; i++)
                        parameterTypes[i + 1] = _targetSignature[i];

                    _signature = new MethodSignature(MethodSignatureFlags.Static, 0, _targetSignature.ReturnType, parameterTypes);
                }
                return _signature;
            }
        }

        public override string Name
        {
            get
            {
                return "CalliMarshallingMethodThunk";
            }
        }

        public override MethodIL EmitIL()
        {
            // TODO
            throw null;
        }
    }
}
