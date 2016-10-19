// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    /// <summary>
    /// Captures information required to generate a ReadyToRun helper to create a delegate type instance
    /// pointing to a specific target method.
    /// </summary>
    public sealed class DelegateCreationInfo
    {
        /// <summary>
        /// Gets the node corresponding to the method that initializes the delegate.
        /// </summary>
        public IMethodNode Constructor
        {
            get;
        }

        /// <summary>
        /// Gets the node representing the target method of the delegate.
        /// </summary>
        public ISymbolNode Target
        {
            get;
        }

        /// <summary>
        /// Gets an optional node passed as an additional argument to the constructor.
        /// </summary>
        public IMethodNode Thunk
        {
            get;
        }

        private DelegateCreationInfo(IMethodNode constructor, ISymbolNode target, IMethodNode thunk = null)
        {
            Constructor = constructor;
            Target = target;
            Thunk = thunk;
        }

        /// <summary>
        /// Constructs a new instance of <see cref="DelegateCreationInfo"/> set up to construct a delegate of type
        /// '<paramref name="delegateType"/>' pointing to '<paramref name="targetMethod"/>'.
        /// </summary>
        public static DelegateCreationInfo Create(TypeDesc delegateType, MethodDesc targetMethod, NodeFactory factory)
        {
            var context = (CompilerTypeSystemContext)delegateType.Context;
            var systemDelegate = targetMethod.Context.GetWellKnownType(WellKnownType.MulticastDelegate).BaseType;

            int paramCountTargetMethod = targetMethod.Signature.Length;
            if (!targetMethod.Signature.IsStatic)
            {
                paramCountTargetMethod++;
            }

            DelegateInfo delegateInfo = context.GetDelegateInfo(delegateType.GetTypeDefinition());
            int paramCountDelegateClosed = delegateInfo.Signature.Length + 1;
            bool closed = false;
            if (paramCountDelegateClosed == paramCountTargetMethod)
            {
                closed = true;
            }
            else
            {
                Debug.Assert(paramCountDelegateClosed == paramCountTargetMethod + 1);
            }

            if (targetMethod.Signature.IsStatic)
            {
                MethodDesc invokeThunk;
                MethodDesc initMethod;

                if (!closed)
                {
                    // Open delegate to a static method
                    invokeThunk = delegateInfo.Thunks[DelegateThunkKind.OpenStaticThunk];
                    initMethod = systemDelegate.GetKnownMethod("InitializeOpenStaticThunk", null);
                }
                else
                {
                    // Closed delegate to a static method (i.e. delegate to an extension method that locks the first parameter)
                    invokeThunk = delegateInfo.Thunks[DelegateThunkKind.ClosedStaticThunk];
                    initMethod = systemDelegate.GetKnownMethod("InitializeClosedStaticThunk", null);
                }

                var instantiatedDelegateType = delegateType as InstantiatedType;
                if (instantiatedDelegateType != null)
                    invokeThunk = context.GetMethodForInstantiatedType(invokeThunk, instantiatedDelegateType);

                return new DelegateCreationInfo(
                    factory.MethodEntrypoint(initMethod),
                    factory.MethodEntrypoint(targetMethod),
                    factory.MethodEntrypoint(invokeThunk));
            }
            else
            {
                if (!closed)
                    throw new NotImplementedException("Open instance delegates");

                bool useUnboxingStub = targetMethod.OwningType.IsValueType;

                string intializeMethodName = "InitializeClosedInstance";
                if (targetMethod.HasInstantiation)
                {
                    Debug.Assert(!targetMethod.IsVirtual, "TODO: delegate to generic virtual method");

                    // Closed delegates to generic instance methods need to be constructed through a slow helper that
                    // checks for the fat function pointer case (function pointer + instantiation argument in a single
                    // pointer) and injects an invocation thunk to unwrap the fat function pointer as part of
                    // the invocation if necessary.
                    intializeMethodName = "InitializeClosedInstanceSlow";
                }

                return new DelegateCreationInfo(
                    factory.MethodEntrypoint(systemDelegate.GetKnownMethod(intializeMethodName, null)),
                    factory.MethodEntrypoint(targetMethod, useUnboxingStub));
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as DelegateCreationInfo;
            return other != null && Constructor == other.Constructor
                && Target == other.Target && Thunk == other.Thunk;
        }

        public override int GetHashCode()
        {
            return Constructor.GetHashCode() ^ Target.GetHashCode();
        }
    }
}
