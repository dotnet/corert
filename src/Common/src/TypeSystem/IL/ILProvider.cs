// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    internal sealed class ILProvider : LockFreeReaderHashtable<MethodDesc, ILProvider.MethodILData>
    {
        private PInvokeILProvider _pinvokeILProvider;

        public ILProvider(PInvokeILProvider pinvokeILProvider)
        {
            _pinvokeILProvider = pinvokeILProvider;
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

        /// <summary>
        /// Provides method bodies for intrinsics recognized by the compiler.
        /// It can return null if it's not an intrinsic recognized by the compiler,
        /// but an intrinsic e.g. recognized by codegen.
        /// </summary>
        private MethodIL TryGetIntrinsicMethodIL(MethodDesc method)
        {
            Debug.Assert(method.IsIntrinsic);

            MetadataType owningType = method.OwningType as MetadataType;
            if (owningType == null)
                return null;

            switch (owningType.Name)
            {
                case "Unsafe":
                    {
                        if (owningType.Namespace == "Internal.Runtime.CompilerServices")
                            return UnsafeIntrinsics.EmitIL(method);
                    }
                    break;
                case "Debug":
                    {
                        if (owningType.Namespace == "System.Diagnostics" && method.Name == "DebugBreak")
                            return new ILStubMethodIL(method, new byte[] { (byte)ILOpcode.break_, (byte)ILOpcode.ret }, Array.Empty<LocalVariableDefinition>(), null);
                    }
                    break;
                case "EETypePtr":
                    {
                        if (owningType.Namespace == "System" && method.Name == "EETypePtrOf")
                            return EETypePtrOfIntrinsic.EmitIL(method);
                    }
                    break;
                case "RuntimeAugments":
                    {
                        if (owningType.Namespace == "Internal.Runtime.Augments" && method.Name == "GetCanonType")
                            return GetCanonTypeIntrinsic.EmitIL(method);
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// Provides method bodies for intrinsics recognized by the compiler that
        /// are specialized per instantiation. It can return null if the intrinsic
        /// is not recognized.
        /// </summary>
        private MethodIL TryGetPerInstantiationIntrinsicMethodIL(MethodDesc method)
        {
            Debug.Assert(method.IsIntrinsic);

            MetadataType owningType = method.OwningType.GetTypeDefinition() as MetadataType;
            if (owningType == null)
                return null;

            string methodName = method.Name;

            switch (owningType.Name)
            {
                case "RuntimeHelpers":
                    {
                        if ((methodName == "IsReferenceOrContainsReferences" || methodName == "IsReference")
                            && owningType.Namespace == "System.Runtime.CompilerServices")
                        {
                            TypeDesc elementType = method.Instantiation[0];

                            // Fallback to non-intrinsic implementation for universal generics
                            if (elementType.IsCanonicalSubtype(CanonicalFormKind.Universal))
                                return null;

                            bool result = elementType.IsGCPointer;
                            if (methodName == "IsReferenceOrContainsReferences")
                            {
                                result |= (elementType.IsDefType ? ((DefType)elementType).ContainsGCPointers : false);
                            }

                            return new ILStubMethodIL(method, new byte[] {
                                    result ? (byte)ILOpcode.ldc_i4_1 : (byte)ILOpcode.ldc_i4_0,
                                    (byte)ILOpcode.ret }, 
                                Array.Empty<LocalVariableDefinition>(), null);
                        }
                    }
                    break;
                case "Comparer`1":
                    {
                        if (methodName == "Create" && owningType.Namespace == "System.Collections.Generic")
                            return ComparerIntrinsics.EmitComparerCreate(method);
                    }
                    break;
                case "EqualityComparer`1":
                    {
                        if (methodName == "Create" && owningType.Namespace == "System.Collections.Generic")
                            return ComparerIntrinsics.EmitEqualityComparerCreate(method);
                    }
                    break;
                case "EqualityComparerHelpers":
                    {
                        if (owningType.Namespace != "Internal.IntrinsicSupport")
                            return null;

                        if (methodName == "EnumOnlyEquals")
                        {
                            // EnumOnlyEquals would basically like to do this:
                            // static bool EnumOnlyEquals<T>(T x, T y) where T: struct => x == y;
                            // This is not legal though.
                            // We don't want to do this:
                            // static bool EnumOnlyEquals<T>(T x, T y) where T: struct => x.Equals(y);
                            // Because it would box y.
                            // So we resort to some per-instantiation magic.

                            TypeDesc elementType = method.Instantiation[0];
                            if (!elementType.IsEnum)
                                return null;

                            ILOpcode convInstruction;
                            if (((DefType)elementType).InstanceFieldSize.AsInt <= 4)
                            {
                                convInstruction = ILOpcode.conv_i4;
                            }
                            else
                            {
                                Debug.Assert(((DefType)elementType).InstanceFieldSize.AsInt == 8);
                                convInstruction = ILOpcode.conv_i8;
                            }

                            return new ILStubMethodIL(method, new byte[] {
                                (byte)ILOpcode.ldarg_0,
                                (byte)convInstruction,
                                (byte)ILOpcode.ldarg_1,
                                (byte)convInstruction,
                                (byte)ILOpcode.prefix1, unchecked((byte)ILOpcode.ceq),
                                (byte)ILOpcode.ret,
                            },
                            Array.Empty<LocalVariableDefinition>(), null);
                        }
                        else if (methodName == "GetComparerForReferenceTypesOnly")
                        {
                            TypeDesc elementType = method.Instantiation[0];
                            if (!elementType.IsRuntimeDeterminedSubtype
                                && !elementType.IsCanonicalSubtype(CanonicalFormKind.Any)
                                && !elementType.IsGCPointer)
                            {
                                return new ILStubMethodIL(method, new byte[] {
                                    (byte)ILOpcode.ldnull,
                                    (byte)ILOpcode.ret
                                },
                                Array.Empty<LocalVariableDefinition>(), null);
                            }
                        }
                        else if (methodName == "StructOnlyEquals")
                        {
                            TypeDesc elementType = method.Instantiation[0];
                            if (!elementType.IsRuntimeDeterminedSubtype
                                && !elementType.IsCanonicalSubtype(CanonicalFormKind.Any)
                                && !elementType.IsGCPointer)
                            {
                                Debug.Assert(elementType.IsValueType);

                                TypeSystemContext context = elementType.Context;
                                MetadataType helperType = context.SystemModule.GetKnownType("Internal.IntrinsicSupport", "EqualityComparerHelpers");

                                MethodDesc methodToCall = null;
                                if (elementType.IsEnum)
                                {
                                    methodToCall = helperType.GetKnownMethod("EnumOnlyEquals", null).MakeInstantiatedMethod(elementType);
                                }
                                else if (elementType.IsNullable && ComparerIntrinsics.ImplementsIEquatable(elementType.Instantiation[0]))
                                {
                                    methodToCall = helperType.GetKnownMethod("StructOnlyEqualsNullable", null).MakeInstantiatedMethod(elementType.Instantiation[0]);
                                }
                                else if (ComparerIntrinsics.ImplementsIEquatable(elementType))
                                {
                                    methodToCall = helperType.GetKnownMethod("StructOnlyEqualsIEquatable", null).MakeInstantiatedMethod(elementType);
                                }

                                if (methodToCall != null)
                                {
                                    return new ILStubMethodIL(method, new byte[]
                                    {
                                        (byte)ILOpcode.ldarg_0,
                                        (byte)ILOpcode.ldarg_1,
                                        (byte)ILOpcode.call, 1, 0, 0, 0,
                                        (byte)ILOpcode.ret
                                    },
                                    Array.Empty<LocalVariableDefinition>(), new object[] { methodToCall });
                                }
                            }
                        }
                    }
                    break;
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
                        return _pinvokeILProvider.EmitIL(method);
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

                return null;
            }
            else
            if (method is MethodForInstantiatedType || method is InstantiatedMethod)
            {
                // Intrinsics specialized per instantiation
                if (method.IsIntrinsic)
                {
                    MethodIL methodIL = TryGetPerInstantiationIntrinsicMethodIL(method);
                    if (methodIL != null)
                        return methodIL;
                }

                var methodDefinitionIL = GetMethodIL(method.GetTypicalMethodDefinition());
                if (methodDefinitionIL == null)
                    return null;
                return new InstantiatedMethodIL(method, methodDefinitionIL);
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
