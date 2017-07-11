// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    public static partial class ThrowHelper
    {
        private static void ThrowTypeLoadException(ExceptionStringID id, string typeName, string assemblyName, string messageArg)
        {
            throw new TypeSystemException.TypeLoadException(id, typeName, assemblyName, messageArg);
        }

        private static void ThrowTypeLoadException(ExceptionStringID id, string typeName, string assemblyName)
        {
            throw new TypeSystemException.TypeLoadException(id, typeName, assemblyName);
        }

        public static void ThrowMissingMethodException(TypeDesc owningType, string methodName, MethodSignature signature)
        {
            throw new TypeSystemException.MissingMethodException(ExceptionStringID.MissingMethod, Format.Method(owningType, methodName, signature));
        }

        public static void ThrowMissingFieldException(TypeDesc owningType, string fieldName)
        {
            throw new TypeSystemException.MissingFieldException(ExceptionStringID.MissingField, Format.Field(owningType, fieldName));
        }

        public static void ThrowFileNotFoundException(ExceptionStringID id, string fileName)
        {
            throw new TypeSystemException.FileNotFoundException(id, fileName);
        }

        public static void ThrowInvalidProgramException()
        {
            throw new TypeSystemException.InvalidProgramException();
        }

        public static void ThrowInvalidProgramException(ExceptionStringID id, MethodDesc method)
        {
            throw new TypeSystemException.InvalidProgramException(id, Format.Method(method));
        }

        public static void ThrowBadImageFormatException()
        {
            throw new TypeSystemException.BadImageFormatException();
        }

        private static partial class Format
        {
            public static string OwningModule(TypeDesc type)
            {
                return Module((type as MetadataType)?.Module);
            }
        }
    }
}
