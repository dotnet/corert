// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    internal enum IntrinsicMethodKind
    {
        None,
        RuntimeHelpersInitializeArray,
    }

    internal class IntrinsicMethods
    {
        public static IntrinsicMethodKind GetIntrinsicMethodClassification(MethodDesc method)
        {
            // TODO: make this reliable
            MetadataType owningMdType = method.OwningType as MetadataType;

            if (owningMdType != null && method.Name == "InitializeArray" && owningMdType.Name == "RuntimeHelpers" && owningMdType.Namespace == "System.Runtime.CompilerServices")
            {
                return IntrinsicMethodKind.RuntimeHelpersInitializeArray;
            }

            return IntrinsicMethodKind.None;
        }

        /// <summary>
        /// NEWOBJ operation on String type is actually a call to a static method that returs a String
        /// instance (i.e. there's an explict call to the runtime allocator from the static method body).
        /// This method returns the alloc+init helper corresponding to a given string constructor.
        /// </summary>
        public static MethodDesc GetStringInitializer(MethodDesc constructorMethod)
        {
            Debug.Assert(constructorMethod.IsConstructor);
            Debug.Assert(constructorMethod.OwningType.IsString);

            // There's an extra (useless) Object as the first arg to match RyuJIT expectations.
            var parameters = new TypeDesc[constructorMethod.Signature.Length + 1];
            parameters[0] = constructorMethod.Context.GetWellKnownType(WellKnownType.Object);
            for (int i = 0; i < constructorMethod.Signature.Length; i++)
                parameters[i + 1] = constructorMethod.Signature[i];

            MethodSignature sig = new MethodSignature(
                MethodSignatureFlags.Static, 0, constructorMethod.OwningType, parameters);

            MethodDesc result = constructorMethod.OwningType.GetMethod("Ctor", sig);

            // TODO: Better exception type. Should be: "CoreLib doesn't have a required thing in it".
            if (result == null)
                throw new NotImplementedException();

            return result;
        }
    }
}
