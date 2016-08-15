// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Threading;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;
using global::Internal.Reflection.Execution;
using global::Internal.Reflection.Core.Execution;

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution.MethodInvokers
{
    //
    // String constructors require special treatment from the compiler, and hence from Reflection invoke as well.
    //
    internal sealed class StringConstructorMethodInvoker : MethodInvoker
    {
        public StringConstructorMethodInvoker(MetadataReader reader, MethodHandle methodHandle)
        {
            // Since we control the definition of System.String, we only do enough analysis of the signature to disambiguate the constructors we support.
            _id = StringConstructorId.None;
            Method method = methodHandle.GetMethod(reader);
            int parameterCount = 0;
            foreach (Handle parameterTypeSignatureHandle in method.Signature.GetMethodSignature(reader).Parameters)
            {
                // If any parameter is a pointer type, bail as we don't support Invokes on pointers.
                if (parameterTypeSignatureHandle.HandleType == HandleType.TypeSpecification)
                {
                    TypeSpecification typeSpecification = parameterTypeSignatureHandle.ToTypeSpecificationHandle(reader).GetTypeSpecification(reader);
                    if (typeSpecification.Signature.HandleType == HandleType.PointerSignature)
                        return;
                }
                parameterCount++;
            }

            switch (parameterCount)
            {
                case 1:
                    _id = StringConstructorId.CharArray;
                    break;

                case 2:
                    _id = StringConstructorId.Char_Int;
                    break;

                case 3:
                    _id = StringConstructorId.CharArray_Int_Int;
                    break;

                default:
                    break;
            }
        }

        public sealed override Object Invoke(Object thisObject, Object[] arguments)
        {
            switch (_id)
            {
                case StringConstructorId.CharArray:
                    {
                        CheckArgumentCount(arguments, 1);
                        char[] value = (char[])(RuntimeAugments.CheckArgument(arguments[0], typeof(char[]).TypeHandle));
                        return new String(value);
                    }

                case StringConstructorId.Char_Int:
                    {
                        CheckArgumentCount(arguments, 2);
                        char c = (char)(RuntimeAugments.CheckArgument(arguments[0], typeof(char).TypeHandle));
                        int count = (int)(RuntimeAugments.CheckArgument(arguments[1], typeof(int).TypeHandle));
                        return new String(c, count);
                    }

                case StringConstructorId.CharArray_Int_Int:
                    {
                        CheckArgumentCount(arguments, 3);
                        char[] value = (char[])(RuntimeAugments.CheckArgument(arguments[0], typeof(char[]).TypeHandle));
                        int startIndex = (int)(RuntimeAugments.CheckArgument(arguments[1], typeof(int).TypeHandle));
                        int length = (int)(RuntimeAugments.CheckArgument(arguments[2], typeof(int).TypeHandle));
                        return new String(value, startIndex, length);
                    }

                default:
                    throw new InvalidOperationException();
            }
        }

        public sealed override Delegate CreateDelegate(RuntimeTypeHandle delegateType, Object target, bool isStatic, bool isVirtual, bool isOpen)
        {
            Debug.Assert(false, "This code should be unreachable. ConstructorInfos do not expose a CreateDelegate().");
            throw NotImplemented.ByDesign;
        }

        private void CheckArgumentCount(Object[] arguments, int expected)
        {
            if (arguments.Length != expected)
                throw new TargetParameterCountException();
        }


        private enum StringConstructorId
        {
            CharArray = 0,          // String(char[])
            Char_Int = 1,           // String(char, int)
            CharArray_Int_Int = 2,  // String(char[], int, int)

            None = -1,
        }

        private StringConstructorId _id;
    }
}

