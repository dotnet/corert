// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using ILCompiler;
using Internal.TypeSystem;

namespace Internal.IL
{
    static class HelperExtensions
    {
        public static MetadataType GetHelperType(this TypeSystemContext context, string name)
        {
            MetadataType helperType = context.SystemModule.GetType("Internal.Runtime.CompilerHelpers", name, false);
            if (helperType == null)
            {
                // TODO: throw the exception that means 'Core Library doesn't have a required thing in it'
                throw new NotImplementedException();
            }

            return helperType;
        }

        public static MethodDesc GetHelperEntryPoint(this TypeSystemContext context, string typeName, string methodName)
        {
            MetadataType helperType = context.GetHelperType(typeName);
            MethodDesc helperMethod = helperType.GetMethod(methodName, null);
            if (helperMethod == null)
            {
                // TODO: throw the exception that means 'Core Library doesn't have a required thing in it'
                throw new NotImplementedException();
            }

            return helperMethod;
        }
    }
}