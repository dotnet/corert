// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

namespace System
{
    internal static class MemberSerializationStringGenerator
    {
        //
        // Generate the "Signature2" binary serialization string for PropertyInfos
        //
        // Because the string is effectively a file format for serialized Reflection objects, it must be exactly correct. If missing
        // metadata prevents generating the string, this method throws a MissingMetadata exception.
        // 
        public static string SerializationToString(this PropertyInfo property)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendSerializationString(property.PropertyType);
            sb.Append(' ');
            sb.Append(property.Name);
            ParameterInfo[] parameters = property.GetIndexParameters();
            if (parameters.Length != 0)
            {
                sb.Append(" [");
                sb.AppendParameters(parameters, isVarArg: false);
                sb.Append(']');
            }
            return sb.ToString();
        }

        //
        // Generate the "Signature2" binary serialization string for ConstructorInfos
        //
        // Because the string is effectively a file format for serialized Reflection objects, it must be exactly correct. If missing
        // metadata prevents generating the string, this method throws a MissingMetadata exception.
        // 
        public static string SerializationToString(this ConstructorInfo constructor)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(constructor.Name);
            sb.Append('(');
            sb.AppendParameters(constructor.GetParametersNoCopy(), constructor.CallingConvention == CallingConventions.VarArgs);
            sb.Append(')');
            return sb.ToString();
        }

        //
        // Generate the "Signature2" binary serialization string for MethodInfos
        //
        // Because the string is effectively a file format for serialized Reflection objects, it must be exactly correct. If missing
        // metadata prevents generating the string, this method throws a MissingMetadata exception.
        // 
        public static string SerializationToString(this MethodInfo method)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendSerializationString(method.ReturnType);
            sb.Append(' ');
            sb.Append(method.Name);
            if (method.IsGenericMethod)
            {
                // Method is a generic method definition or a constructed generic method. Either way, the emit the generic parameters or arguments in brackets.
                sb.AppendGenericTypeArguments(method.GetGenericArguments());
            }
            sb.Append('(');
            sb.AppendParameters(method.GetParametersNoCopy(), method.CallingConvention == CallingConventions.VarArgs);
            sb.Append(')');
            return sb.ToString();
        }

        //
        // Generated the Signature2 substring for the parameters of a method, constructor or property.
        //
        private static void AppendParameters(this StringBuilder sb, ParameterInfo[] parameters, bool isVarArg)
        {
            string comma = string.Empty;
            for (int i = 0; i < parameters.Length; i++)
            {
                sb.Append(comma);
                sb.AppendSerializationString(parameters[i].ParameterType, withinGenericTypeArgument: false);
                comma = ", ";
            }

            if (isVarArg)
            {
                sb.Append(comma);
                sb.Append("...");
            }
        }

        //
        // Generate the "Signature2" binary serialization string for Type objects appearing inside serialized MethodBase and PropertyInfo signatures.
        //
        // Because the string is effectively a file format for serialized Reflection objects, it must be exactly correct. If missing
        // metadata prevents generating the string, this method throws a MissingMetadata exception.
        //
        // "withinGenericArgument" is used to track whether we're in the middle of serializing a generic type argument.
        // Some of the string-generation rules change when in that state:
        //
        //    - Generic type parameters no longer get prepended with "!" or "!!".
        //    - Plain old types are serialized as assembly-qualified names enclosed in square brackets.
        //
        private static void AppendSerializationString(this StringBuilder sb, Type type, bool withinGenericTypeArgument = false)
        {
            if (type.HasElementType)
            {
                sb.AppendSerializationString(type.GetElementType(), withinGenericTypeArgument);
                if (type.IsSZArray)
                {
                    sb.Append("[]");
                }
                else if (type.IsVariableBoundArray)
                {
                    sb.Append('[');
                    int rank = type.GetArrayRank();
                    if (rank == 1)
                    {
                        sb.Append('*');
                    }
                    else
                    {
                        sb.Append(',', rank - 1);
                    }
                    sb.Append(']');
                }
                else if (type.IsByRef)
                {
                    sb.Append('&');
                }
                else if (type.IsPointer)
                {
                    sb.Append('*');
                }
                else
                {
                    Debug.Fail("Should not get here.");
                    throw new InvalidOperationException(); //Unexpected error: Runtime Reflection is a trusted source so we should not have gotten here.
                }
            }
            else if (type.IsGenericParameter)
            {
                if (!withinGenericTypeArgument)
                {
                    // This special rule causes generic type variables ("T") to serialize as "!T" (variable on type) or "!!T" (variable on method)
                    // to distinguish them from a plain old type named "T".
                    //
                    // This rule does not kick in if we're serializing a type variable embedded inside a generic type argument list. (Fortunately, there 
                    // is no risk of ambiguity in that case because generic type argument lists always serialize plain old types as "[<assembly-qualified-name>]".) 
                    sb.Append('!');
                    if (type.DeclaringMethod != null)
                    {
                        sb.Append('!');
                    }
                }
                sb.Append(type.Name);
            }
            else
            {
                // If we got here, "type" is either a plain old type or a constructed generic type.

                if (withinGenericTypeArgument)
                {
                    sb.Append('[');
                }

                Type plainOldType;
                Type[] instantiation;
                SplitIntoPlainOldTypeAndInstantiation(type, out plainOldType, out instantiation);
                sb.Append(plainOldType.FullName);
                if (instantiation != null)
                {
                    sb.AppendGenericTypeArguments(instantiation);
                }
                if (withinGenericTypeArgument)
                {
                    sb.Append(", ");
                    sb.Append(plainOldType.Assembly.FullName);
                    sb.Append(']');
                }
            }
        }

        private static void AppendGenericTypeArguments(this StringBuilder sb, Type[] genericTypeArguments)
        {
            sb.Append('[');
            for (int i = 0; i < genericTypeArguments.Length; i++)
            {
                if (i != 0)
                {
                    sb.Append(',');
                }
                sb.AppendSerializationString(genericTypeArguments[i], withinGenericTypeArgument: true);
            }
            sb.Append(']');
        }

        /// <summary>
        /// Sets "instatiation" to null if there are no generic type arguments to serialize. Do a full framework quirk, this is not equivalent
        /// to testing Type.IsConstructedGenericType.
        /// </summary>
        private static void SplitIntoPlainOldTypeAndInstantiation(Type type, out Type plainOldType, out Type[] instantiation)
        {
            if (!type.IsConstructedGenericType)
            {
                plainOldType = type;
                instantiation = null;
                return;
            }

            plainOldType = type.GetGenericTypeDefinition();
            instantiation = type.GenericTypeArguments;

            // Check for a special case for compatibility: if the generic type arguments are exactly the generic type parameters, serialize it
            // as if the type was simply the generic type definition itself.

            // First, a quick pass to eliminate the common case before we incur an allocation (via GenericTypeParameters.)
            foreach (Type genericTypeArgument in instantiation)
            {
                if (!genericTypeArgument.IsGenericParameter)
                    return;
            }

            Type[] genericTypeParameters = plainOldType.GetTypeInfo().GenericTypeParameters;
            Debug.Assert(genericTypeParameters.Length == instantiation.Length); // This invariant is guaranteed by Reflection.

            for (int i = 0; i < instantiation.Length; i++)
            {
                if (!(instantiation[i].Equals(genericTypeParameters[i])))
                    return;
            }

            // If we got here, we hit the special case. Just serialize it as a plain old (generic) type. Never mind that such a thing cannot legally appear
            // in a method signature - this is an artifact of how the desktop represents these things.
            instantiation = null;
        }
    }
}
