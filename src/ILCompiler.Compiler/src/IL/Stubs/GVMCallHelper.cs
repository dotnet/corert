// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Collections.Generic;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;
using System.Threading;

namespace Internal.IL.Stubs
{
    internal class GVMCallHelper: ILStubMethod
    {
        private MethodDesc _targetMethod;
        private MethodSignature _signature;
        private TypeDesc[] _instantiation;

        public GVMCallHelper(TypeDesc owningType, MethodDesc targetMethod)
        {
            Debug.Assert(targetMethod == targetMethod.GetTypicalMethodDefinition());

            OwningType = owningType;
            _targetMethod = targetMethod;
        }

        public override TypeSystemContext Context => _targetMethod.Context;
        public override TypeDesc OwningType { get; }
        public override string Name => $"GVMCallHelper_{NodeFactory.NameMangler.GetMangledMethodName(_targetMethod)}";

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                {
                    TypeDesc[] parameters = new TypeDesc[_targetMethod.Signature.Length + 1];

                    // This pointer
                    parameters[0] = _targetMethod.Context.GetWellKnownType(WellKnownType.Object);

                    // Rest of the parameters
                    for (int i = 0; i < _targetMethod.Signature.Length; i++)
                        parameters[i + 1] = _targetMethod.Signature[i];

                    _signature = new MethodSignature(
                        MethodSignatureFlags.Static,
                        _targetMethod.OwningType.Instantiation.Length + _targetMethod.Instantiation.Length,
                        _targetMethod.Signature.ReturnType,
                        parameters);
                }

                return _signature;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                if (_instantiation == null)
                {
                    int genericParameterCount = _targetMethod.OwningType.Instantiation.Length + _targetMethod.Instantiation.Length;

                    TypeDesc[] instantiation = new TypeDesc[genericParameterCount];
                    for (int i = 0; i < genericParameterCount; i++)
                        instantiation[i] = new GVMHelperGenericParameter(Context, i);

                    Interlocked.CompareExchange(ref _instantiation, instantiation, null);
                }

                return new Instantiation(_instantiation);
            }
        }

        public MethodDesc GetInstantiatedGVMTarget(Instantiation stubInstantiationArgs)
        {
            Debug.Assert(stubInstantiationArgs.Length == (_targetMethod.OwningType.Instantiation.Length + _targetMethod.Instantiation.Length));

            int index = 0;
            MetadataType gvmType = (MetadataType)_targetMethod.OwningType;
            if (gvmType.HasInstantiation)
            {
                TypeDesc[] gvmTypeInstantiation = new TypeDesc[gvmType.Instantiation.Length];
                for (int i = 0; i < gvmTypeInstantiation.Length; i++)
                    gvmTypeInstantiation[i] = stubInstantiationArgs[index++];

                gvmType = gvmType.MakeInstantiatedType(gvmTypeInstantiation);
            }

            MethodDesc gvmMethod = gvmType.GetMethod(_targetMethod.Name, _targetMethod.Signature);

            TypeDesc[] gvmMethodInstantiation = new TypeDesc[_targetMethod.Instantiation.Length];
            for (int i = 0; i < gvmMethodInstantiation.Length; i++)
                gvmMethodInstantiation[i] = stubInstantiationArgs[index++];

            return gvmMethod.MakeInstantiatedMethod(gvmMethodInstantiation);
        }

        public override MethodIL EmitIL()
        {
            ILEmitter emitter = new ILEmitter();
            ILCodeStream codeStream = emitter.NewCodeStream();

            TypeDesc[] instantiationArgs = new TypeDesc[_targetMethod.OwningType.Instantiation.Length + _targetMethod.Instantiation.Length];
            for (int i = 0; i < instantiationArgs.Length; i++)
                instantiationArgs[i] = Context.GetSignatureVariable(i, true);
            MethodDesc gvmMethod = GetInstantiatedGVMTarget(new Instantiation(instantiationArgs));

            MethodDesc GVMLookupForSlot = Context.SystemModule.GetKnownType("System.Runtime", "TypeLoaderExports").GetKnownMethod("GVMLookupForSlot", null);

            emitter.NewLocal(Context.GetWellKnownType(WellKnownType.IntPtr));

            // Call the GVMLookupForSlot helper to resolve the GVM target
            //  First arg = this pointer
            //  Secont arg = RuntimeMethodHandle of GVM target
            codeStream.EmitLdArg(0);
            codeStream.Emit(ILOpcode.ldtoken, emitter.NewToken(gvmMethod));
            codeStream.Emit(ILOpcode.call, emitter.NewToken(GVMLookupForSlot));
            codeStream.EmitStLoc(0);

            // Load the args on the stack
            int argIndex = 0;
            while (argIndex < Signature.Length)
                codeStream.EmitLdArg(argIndex++);

            // Load the target method pointer
            codeStream.EmitLdLoc(0);

            // Emit the calli
            CalliIntrinsic.EmitTransformedCalli(emitter, codeStream, gvmMethod.Signature);
            //codeStream.Emit(ILOpcode.calli, emitter.NewToken(gvmMethod.Signature));

            codeStream.Emit(ILOpcode.ret);

            return emitter.Link(this);
        }

        private class GVMHelperGenericParameter : GenericParameterDesc
        {
            public GVMHelperGenericParameter(TypeSystemContext context, int index)
            {
                Context = context;
                Index = index;
                Kind = GenericParameterKind.Method;
            }

            public override TypeSystemContext Context { get; }
            public override int Index { get; }
            public override GenericParameterKind Kind { get; }
            public override string Name => $"T{Index}";

            public override string ToString()
            {
                return Name;
            }
        }
    }

    internal class GVMCallHelperCache
    {
        private TypeDesc _owningTypeForHelpers;
        private Dictionary<MethodDesc, MethodDesc> _cache;

        public GVMCallHelperCache(TypeDesc owningTypeForHelpers)
        {
            _owningTypeForHelpers = owningTypeForHelpers;
            _cache = new Dictionary<MethodDesc, MethodDesc>();
        }

        public MethodDesc GetHelper(MethodDesc targetMethod)
        {
            MethodDesc result;
            if (!_cache.TryGetValue(targetMethod.GetTypicalMethodDefinition(), out result))
            {
                result = new GVMCallHelper(_owningTypeForHelpers, targetMethod.GetTypicalMethodDefinition());
                _cache[targetMethod.GetTypicalMethodDefinition()] = result;
            }

            int numGenericArgs = targetMethod.OwningType.Instantiation.Length + targetMethod.Instantiation.Length;
            TypeDesc[] genericArgs = new TypeDesc[numGenericArgs];
            for (int i = 0; i < numGenericArgs; i++)
            {
                if (i >= targetMethod.OwningType.Instantiation.Length)
                    genericArgs[i] = targetMethod.Instantiation[i - targetMethod.OwningType.Instantiation.Length];
                else
                    genericArgs[i] = targetMethod.OwningType.Instantiation[i];
            }

            return result.MakeInstantiatedMethod(genericArgs);
        }
    }
}