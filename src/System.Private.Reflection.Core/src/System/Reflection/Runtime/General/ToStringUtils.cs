// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection.Runtime.TypeInfos;
using Internal.Reflection.Core.Execution;

namespace System.Reflection.Runtime.General
{
    internal static class ToStringUtils
    {
        // 
        // This is a port of the desktop CLR's RuntimeType.FormatTypeName() routine. This routine is used by various Reflection ToString() methods
        // to display the name of a type. Do not use for any other purpose as it inherits some pretty quirky desktop behavior.
        //
        // The Project N version takes a raw metadata handle rather than a completed type so that it remains robust in the face of missing metadata.
        //
        public static String FormatTypeName(this QTypeDefRefOrSpec qualifiedTypeHandle, TypeContext typeContext)
        {
            try
            {
                // Though we wrap this in a try-catch as a failsafe, this code must still strive to avoid triggering MissingMetadata exceptions
                // (non-error exceptions are very annoying when debugging.)

                Exception exception = null;
                RuntimeTypeInfo runtimeType = qualifiedTypeHandle.TryResolve(typeContext, ref exception);
                if (runtimeType == null)
                    return UnavailableType;

                // Because this runtimeType came from a successful TryResolve() call, it is safe to querying the TypeInfo's of the type and its component parts.
                // If we're wrong, we do have the safety net of a try-catch.
                return runtimeType.FormatTypeName();
            }
            catch (Exception)
            {
                return UnavailableType;
            }
        }

        // 
        // This is a port of the desktop CLR's RuntimeType.FormatTypeName() routine. This routine is used by various Reflection ToString() methods
        // to display the name of a type. Do not use for any other purpose as it inherits some pretty quirky desktop behavior.
        //
        // The Project N version takes a raw metadata handle rather than a completed type so that it remains robust in the face of missing metadata.
        //
        public static String FormatTypeName(this RuntimeTypeInfo runtimeType)
        {
            try
            {
                // Though we wrap this in a try-catch as a failsafe, this code must still strive to avoid triggering MissingMetadata exceptions
                // (non-error exceptions are very annoying when debugging.)

                // Legacy: this doesn't make sense, why use only Name for nested types but otherwise
                // ToString() which contains namespace.
                RuntimeTypeInfo rootElementType = runtimeType;
                while (rootElementType.HasElementType)
                    rootElementType = rootElementType.InternalRuntimeElementType;
                if (rootElementType.IsNested)
                {
                    String name = runtimeType.InternalNameIfAvailable;
                    return name == null ? UnavailableType : name;
                }

                // Legacy: why removing "System"? Is it just because C# has keywords for these types?
                // If so why don't we change it to lower case to match the C# keyword casing?
                String typeName = runtimeType.ToString();
                if (typeName.StartsWith("System."))
                {
                    foreach (Type pt in ReflectionCoreExecution.ExecutionDomain.PrimitiveTypes)
                    {
                        if (pt.Equals(rootElementType) || rootElementType.Equals(CommonRuntimeTypes.Void))
                        {
                            typeName = typeName.Substring("System.".Length);
                            break;
                        }
                    }
                }
                return typeName;
            }
            catch (Exception)
            {
                return UnavailableType;
            }
        }

        public const String UnavailableType = "UnknownType";
    }
}
