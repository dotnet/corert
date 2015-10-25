// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILToNative
{
    enum IntrinsicMethodKind
    {
        None,
        RuntimeHelpersInitializeArray,
    }

    class IntrinsicMethods
    {
        public static IntrinsicMethodKind GetIntrinsicMethodClassification(MethodDesc method)
        {
            // TODO: make this reliable
            if (method.Name == "InitializeArray" && method.OwningType.Name == "System.Runtime.CompilerServices.RuntimeHelpers")
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

            MethodSignatureBuilder builder = new MethodSignatureBuilder(constructorMethod.Signature);
            builder.Flags = MethodSignatureFlags.Static;
            builder.ReturnType = constructorMethod.OwningType;
            MethodDesc result = constructorMethod.OwningType.GetMethod("Ctor", builder.ToSignature());

            // TODO: Better exception type. Should be: "CoreLib doesn't have a required thing in it".
            if (result == null)
                throw new NotImplementedException();

            return result;
        }
    }
}
