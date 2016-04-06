// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.Assemblies;
using global::System.Reflection.Runtime.TypeParsing;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

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
        public static String FormatTypeName(this Handle typeDefRefOrSpecHandle, MetadataReader reader, TypeContext typeContext, ReflectionDomain reflectionDomain)
        {
            try
            {
                // Though we wrap this in a try-catch as a failsafe, this code must still strive to avoid triggering MissingMetadata exceptions
                // (non-error exceptions are very annoying when debugging.)

                Exception exception = null;
                RuntimeType runtimeType = reflectionDomain.TryResolve(reader, typeDefRefOrSpecHandle, typeContext, ref exception);
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
        public static String FormatTypeName(this RuntimeType runtimeType)
        {
            try
            {
                // Though we wrap this in a try-catch as a failsafe, this code must still strive to avoid triggering MissingMetadata exceptions
                // (non-error exceptions are very annoying when debugging.)

                ReflectionDomain reflectionDomain = runtimeType.GetReflectionDomain();

                // Legacy: this doesn't make sense, why use only Name for nested types but otherwise
                // ToString() which contains namespace.
                RuntimeType rootElementType = runtimeType;
                while (rootElementType.HasElementType)
                    rootElementType = rootElementType.InternalRuntimeElementType;
                if (rootElementType.IsNested)
                {
                    String name = runtimeType.InternalNameIfAvailable;
                    return name == null ? UnavailableType : name;
                }

                // Legacy: why removing "System"? Is it just because C# has keywords for these types?
                // If so why don't we change it to lower case to match the C# keyword casing?
                FoundationTypes foundationTypes = reflectionDomain.FoundationTypes;
                String typeName = runtimeType.ToString();
                if (typeName.StartsWith("System."))
                {
                    foreach (Type pt in reflectionDomain.PrimitiveTypes)
                    {
                        if (pt.Equals(rootElementType) || rootElementType.Equals(foundationTypes.SystemVoid))
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
