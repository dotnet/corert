// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        private MethodIL TryGetRuntimeImplementedMethodIL(MethodDesc method)
        {
            // Provides method bodies for runtime implemented methods. It can return null for
            // methods that are treated specially by the codegen.

            Debug.Assert(method.IsRuntimeImplemented);

            TypeDesc owningType = method.OwningType;

            if (owningType.IsDelegate)
            {
                return DelegateMethodILEmitter.EmitIL(method);
            }

            return null;
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
                return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.ldarg_0, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
            }
            else
            if ((methodName == "CompareExchange" || methodName == "Exchange") && method.HasInstantiation && owningType.Name == "Interlocked" && owningType.Namespace == "System.Threading")
            {
                // TODO: Replace with regular implementation once ref locals are available in C# (https://github.com/dotnet/roslyn/issues/118)
                return InterlockedIntrinsic.EmitIL(method);
            }
            else
            if (methodName == "EETypePtrOf" && owningType.Name == "EETypePtr" && owningType.Namespace == "System")
            {
                return EETypePtrOfIntrinsic.EmitIL(method);
            }
            else if (owningType.Name == "InvokeUtils" && owningType.Namespace == "System")
            {
                return InvokeUtilsIntrinsics.EmitIL(method);
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
                        return PInvokeILEmitter.EmitIL(method);
                    method = pregenerated;
                }

                if (method.IsRuntimeImplemented)
                {
                    MethodIL result = TryGetRuntimeImplementedMethodIL(method);
                    if (result != null)
                        return result;
                }

                MethodIL methodIL = EcmaMethodIL.Create((EcmaMethod)method);
                if (methodIL != null)
                    return methodIL;
                
                if (!method.IsInternalCall && !method.IsRuntimeImplemented)
                {
                    return MissingMethodBodyILEmitter.EmitIL(method);
                }

                return null;
            }
            else
            if (method is MethodForInstantiatedType || method is InstantiatedMethod)
            {
                if (method.IsIntrinsic && method.Name == "CreateInstanceIntrinsic")
                {
                    // CreateInstanceIntrinsic is specialized per instantiation
                    return CreateInstanceIntrinsic.EmitIL(method);
                }

                var methodDefinitionIL = GetMethodIL(method.GetTypicalMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(method, methodDefinitionIL, method.OwningType.Instantiation, method.Instantiation);
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
                Debug.Assert(!(method is PInvokeTargetNativeMethod), "Who is asking for IL of PInvokeTargetNativeMethod?");
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
