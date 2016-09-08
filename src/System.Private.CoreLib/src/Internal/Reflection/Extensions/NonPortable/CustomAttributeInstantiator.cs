// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Internal.Runtime.Augments;

//==================================================================================================================
// Dependency note:
//   This class must depend only on the CustomAttribute properties that return IEnumerable<CustomAttributeData>.
//   All of the other custom attribute api route back here so calls to them will cause an infinite recursion.
//==================================================================================================================

namespace Internal.Reflection.Extensions.NonPortable
{
    public static class CustomAttributeInstantiator
    {
        //
        // Turn a CustomAttributeData into a live Attribute object. There's nothing actually non-portable about this one,
        // however, it is included as a concession to that the fact the Reflection.Execution which implements this contract
        // also needs this functionality to implement default values, and we don't want to duplicate this code.
        //
        public static Attribute Instantiate(this CustomAttributeData cad)
        {
            if (cad == null)
                return null;
            Type attributeType = cad.AttributeType;
            TypeInfo attributeTypeInfo = attributeType.GetTypeInfo();

            //
            // Find the public constructor that matches the supplied arguments.
            //
            ConstructorInfo matchingCtor = null;
            ParameterInfo[] matchingParameters = null;
            IList<CustomAttributeTypedArgument> constructorArguments = cad.ConstructorArguments;
            foreach (ConstructorInfo ctor in attributeTypeInfo.DeclaredConstructors)
            {
                if ((ctor.Attributes & (MethodAttributes.Static | MethodAttributes.MemberAccessMask)) != (MethodAttributes.Public))
                    continue;

                ParameterInfo[] parameters = ctor.GetParametersNoCopy();
                if (parameters.Length != constructorArguments.Count)
                    continue;
                int i;
                for (i = 0; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;
                    if (!(parameterType.Equals(constructorArguments[i].ArgumentType) ||
                          parameterType.Equals(typeof(Object))))
                        break;
                }
                if (i == parameters.Length)
                {
                    matchingCtor = ctor;
                    matchingParameters = parameters;
                    break;
                }
            }
            if (matchingCtor == null)
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(attributeTypeInfo); // No matching ctor.

            //
            // Found the right constructor. Instantiate the Attribute.
            //
            int arity = matchingParameters.Length;
            Object[] invokeArguments = new Object[arity];
            for (int i = 0; i < arity; i++)
            {
                invokeArguments[i] = constructorArguments[i].Convert();
            }
            Attribute newAttribute = (Attribute)(matchingCtor.Invoke(invokeArguments));

            //
            // If there any named arguments, evaluate them and set the appropriate field or property.
            //
            foreach (CustomAttributeNamedArgument namedArgument in cad.NamedArguments)
            {
                Object argumentValue = namedArgument.TypedValue.Convert();
                TypeInfo walk = attributeTypeInfo;
                String name = namedArgument.MemberName;
                if (namedArgument.IsField)
                {
                    // Field
                    for (;;)
                    {
                        FieldInfo fieldInfo = walk.GetDeclaredField(name);
                        if (fieldInfo != null)
                        {
                            fieldInfo.SetValue(newAttribute, argumentValue);
                            break;
                        }
                        Type baseType = walk.BaseType;
                        if (baseType == null)
                            throw RuntimeAugments.Callbacks.CreateMissingMetadataException(attributeTypeInfo); // No field matches named argument.
                        walk = baseType.GetTypeInfo();
                    }
                }
                else
                {
                    // Property
                    for (;;)
                    {
                        PropertyInfo propertyInfo = walk.GetDeclaredProperty(name);
                        if (propertyInfo != null)
                        {
                            propertyInfo.SetValue(newAttribute, argumentValue);
                            break;
                        }
                        Type baseType = walk.BaseType;
                        if (baseType == null)
                            throw RuntimeAugments.Callbacks.CreateMissingMetadataException(attributeTypeInfo); // No field matches named argument.
                        walk = baseType.GetTypeInfo();
                    }
                }
            }

            return newAttribute;
        }

        //
        // Convert the argument value reported by Reflection into an actual object.
        //
        private static Object Convert(this CustomAttributeTypedArgument typedArgument)
        {
            Type argumentType = typedArgument.ArgumentType;
            if (!argumentType.IsArray)
            {
                bool isEnum = argumentType.GetTypeInfo().IsEnum;
                Object argumentValue = typedArgument.Value;
                if (isEnum)
                    argumentValue = Enum.ToObject(argumentType, argumentValue);
                return argumentValue;
            }
            else
            {
                IList<CustomAttributeTypedArgument> typedElements = (IList<CustomAttributeTypedArgument>)(typedArgument.Value);
                if (typedElements == null)
                    return null;
                Type elementType = argumentType.GetElementType();
                Array array = Array.CreateInstance(elementType, typedElements.Count);
                for (int i = 0; i < typedElements.Count; i++)
                {
                    Object elementValue = typedElements[i].Convert();
                    array.SetValue(elementValue, i);
                }
                return array;
            }
        }

        //
        // Only public instance fields can be targets of named arguments.
        //
        private static bool IsValidNamedArgumentTarget(this FieldInfo fieldInfo)
        {
            if ((fieldInfo.Attributes & (FieldAttributes.FieldAccessMask | FieldAttributes.Static | FieldAttributes.Literal)) !=
                FieldAttributes.Public)
                return false;
            return true;
        }

        //
        // Only public read/write instance properties can be targets of named arguments.
        //
        private static bool IsValidNamedArgumentTarget(this PropertyInfo propertyInfo)
        {
            MethodInfo getter = propertyInfo.GetMethod;
            MethodInfo setter = propertyInfo.SetMethod;
            if (getter == null)
                return false;
            if ((getter.Attributes & (MethodAttributes.Static | MethodAttributes.MemberAccessMask)) != MethodAttributes.Public)
                return false;
            if (setter == null)
                return false;
            if ((setter.Attributes & (MethodAttributes.Static | MethodAttributes.MemberAccessMask)) != MethodAttributes.Public)
                return false;
            return true;
        }
    }
}

