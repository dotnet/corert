// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    internal sealed class ILProvider : LockFreeReaderHashtable<MethodDesc, ILProvider.MethodILData>
    {
        public ILProvider()
        {
        }

        private MethodIL TryGetIntrinsicMethodIL(MethodDesc method)
        {
            // Provides method bodies for intrinsics recognized by the compiler.
            // It can return null if it's not an intrinsic recognized by the compiler,
            // but an intrinsic e.g. recognized by codegen.

            Debug.Assert(method.IsIntrinsic);
            MetadataType owningType = method.OwningType as MetadataType;
            if (owningType == null)
                return null;

            string methodName = method.Name;

            if (methodName == "UncheckedCast" && owningType.Name == "RuntimeHelpers" && owningType.Namespace == "System.Runtime.CompilerServices")
            {
                return new ILStubMethodIL(new byte[] { (byte)ILOpcode.ldarg_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
            }
            else
            if ((methodName == "CompareExchange" || methodName == "Exchange") && method.HasInstantiation && owningType.Name == "Interlocked" && owningType.Namespace == "System.Threading")
            {
                // TODO: Replace with regular implementation once ref locals are available in C# (https://github.com/dotnet/roslyn/issues/118)
                return InterlockedIntrinsic.EmitIL(method);
            }

            return null;
        }

        private MethodIL CreateMethodIL(MethodDesc method)
        {
            if (method is EcmaMethod)
            {
                // TODO: Workaround: we should special case methods with Intrinsic attribute, but since
                //       CoreLib source is still not in the repo, we have to work with what we have, which is
                //       an MCG attribute on the type itself...
                if (((MetadataType)method.OwningType).HasCustomAttribute("System.Runtime.InteropServices", "McgIntrinsicsAttribute"))
                {
                    var name = method.Name;
                    if (name == "Call")
                    {
                        return CalliIntrinsic.EmitIL(method);
                    }
                    else
                    if (name == "AddrOf")
                    {
                        return AddrOfIntrinsic.EmitIL(method);
                    }
                }

                if (method.IsIntrinsic)
                {
                    MethodIL result = TryGetIntrinsicMethodIL(method);
                    if (result != null)
                        return result;
                }

                if (method.IsPInvoke)
                {
                    var pregenerated = McgInteropSupport.TryGetPregeneratedPInvoke(method);
                    if (pregenerated == null)
                        return PInvokeMarshallingILEmitter.EmitIL(method);
                    method = pregenerated;
                }

                return EcmaMethodIL.Create((EcmaMethod)method);
            }
            else
            if (method is MethodForInstantiatedType)
            {
                var methodDefinitionIL = GetMethodIL(method.GetTypicalMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(methodDefinitionIL, method.OwningType.Instantiation, new Instantiation());
            }
            else
            if (method is InstantiatedMethod)
            {
                var methodDefinitionIL = GetMethodIL(method.GetMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(methodDefinitionIL, new Instantiation(), method.Instantiation);
            }
            else
            if (method is ILStubMethod)
            {
                return ((ILStubMethod)method).EmitIL();
            }
            else
            if (method is ArrayMethod)
            {
                return ArrayMethodILEmitter.EmitIL((ArrayMethod)method);
            }
            else
            {
                return null;
            }
        }

        internal class MethodILData
        {
            public MethodDesc Method;
            public MethodIL MethodIL;
        }
        protected override int GetKeyHashCode(MethodDesc key)
        {
            return key.GetHashCode();
        }
        protected override int GetValueHashCode(MethodILData value)
        {
            return value.Method.GetHashCode();
        }
        protected override bool CompareKeyToValue(MethodDesc key, MethodILData value)
        {
            return Object.ReferenceEquals(key, value.Method);
        }
        protected override bool CompareValueToValue(MethodILData value1, MethodILData value2)
        {
            return Object.ReferenceEquals(value1.Method, value2.Method);
        }
        protected override MethodILData CreateValueFromKey(MethodDesc key)
        {
            return new MethodILData() { Method = key, MethodIL = CreateMethodIL(key) };
        }

        public MethodIL GetMethodIL(MethodDesc method)
        {
            return GetOrCreateValue(method).MethodIL;
        }
    }
}
