// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

using ReflectionEventSource = System.Reflection.Runtime.Tracing.ReflectionEventSource;

namespace Internal.Reflection.Tracing
{
    //
    // The individual event methods. These are in a separate file to allow them to be tool-generated.
    //
    public static partial class ReflectionTrace
    {
        public static void Type_MakeGenericType(Type type, Type[] typeArguments)
        {
            String typeName = type.NameString();
            if (typeName == null)
                return;
            String typeArgumentStrings = typeArguments.GenericTypeArgumentStrings();
            if (typeArgumentStrings == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakeGenericType(typeName, typeArgumentStrings);
        }

        public static void Type_MakeArrayType(Type type)
        {
            String typeName = type.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakeArrayType(typeName);
        }

        public static void Type_FullName(Type type)
        {
            String typeName = type.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_FullName(typeName);
        }

        public static void Type_Namespace(Type type)
        {
            String typeName = type.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_Namespace(typeName);
        }

        public static void Type_AssemblyQualifiedName(Type type)
        {
            String typeName = type.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_AssemblyQualifiedName(typeName);
        }

        public static void Type_Name(Type type)
        {
            String typeName = type.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_Name(typeName);
        }

        public static void TypeInfo_CustomAttributes(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_CustomAttributes(typeName);
        }

        public static void TypeInfo_Name(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_Name(typeName);
        }

        public static void TypeInfo_BaseType(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_BaseType(typeName);
        }

        public static void TypeInfo_DeclaredConstructors(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredConstructors(typeName);
        }

        public static void TypeInfo_DeclaredEvents(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredEvents(typeName);
        }

        public static void TypeInfo_DeclaredFields(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredFields(typeName);
        }

        public static void TypeInfo_DeclaredMembers(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredMembers(typeName);
        }

        public static void TypeInfo_DeclaredMethods(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredMethods(typeName);
        }

        public static void TypeInfo_DeclaredNestedTypes(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredNestedTypes(typeName);
        }

        public static void TypeInfo_DeclaredProperties(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaredProperties(typeName);
        }

        public static void TypeInfo_DeclaringMethod(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_DeclaringMethod(typeName);
        }

        public static void TypeInfo_FullName(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_FullName(typeName);
        }

        public static void TypeInfo_AssemblyQualifiedName(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_AssemblyQualifiedName(typeName);
        }

        public static void TypeInfo_Namespace(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_Namespace(typeName);
        }

        public static void TypeInfo_GetDeclaredEvent(TypeInfo typeInfo, String eventName)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            if (eventName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_GetDeclaredEvent(typeName, eventName);
        }

        public static void TypeInfo_GetDeclaredField(TypeInfo typeInfo, String fieldName)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            if (fieldName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_GetDeclaredField(typeName, fieldName);
        }

        public static void TypeInfo_GetDeclaredMethod(TypeInfo typeInfo, String methodName)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            if (methodName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_GetDeclaredMethod(typeName, methodName);
        }

        public static void TypeInfo_GetDeclaredProperty(TypeInfo typeInfo, String propertyName)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_GetDeclaredProperty(typeName, propertyName);
        }

        public static void TypeInfo_MakeArrayType(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakeArrayType(typeName);
        }

        public static void TypeInfo_MakeArrayType(TypeInfo typeInfo, int rank)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakeArrayType(typeName);
        }

        public static void TypeInfo_MakeByRefType(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakeByRefType(typeName);
        }

        public static void TypeInfo_MakeGenericType(TypeInfo typeInfo, Type[] typeArguments)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            String typeArgumentStrings = typeArguments.GenericTypeArgumentStrings();
            if (typeArgumentStrings == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakeGenericType(typeName, typeArgumentStrings);
        }

        public static void TypeInfo_MakePointerType(TypeInfo typeInfo)
        {
            String typeName = typeInfo.NameString();
            if (typeName == null)
                return;
            ReflectionEventSource.Log.TypeInfo_MakePointerType(typeName);
        }

        public static void Assembly_DefinedTypes(Assembly assembly)
        {
            String assemblyName = assembly.NameString();
            if (assemblyName == null)
                return;
            ReflectionEventSource.Log.Assembly_DefinedTypes(assemblyName);
        }

        public static void Assembly_GetType(Assembly assembly, String typeName)
        {
            String assemblyName = assembly.NameString();
            if (assemblyName == null)
                return;
            if (typeName == null)
                return;
            ReflectionEventSource.Log.Assembly_GetType(assemblyName, typeName);
        }

        public static void Assembly_CustomAttributes(Assembly assembly)
        {
            String assemblyName = assembly.NameString();
            if (assemblyName == null)
                return;
            ReflectionEventSource.Log.Assembly_CustomAttributes(assemblyName);
        }

        public static void Assembly_FullName(Assembly assembly)
        {
            String assemblyName = assembly.NameString();
            if (assemblyName == null)
                return;
            ReflectionEventSource.Log.Assembly_FullName(assemblyName);
        }

        public static void Assembly_GetName(Assembly assembly)
        {
            String assemblyName = assembly.NameString();
            if (assemblyName == null)
                return;
            ReflectionEventSource.Log.Assembly_GetName(assemblyName);
        }

        public static void CustomAttributeData_ConstructorArguments(CustomAttributeData customAttributeData)
        {
            String attributeTypeName = customAttributeData.AttributeTypeNameString();
            if (attributeTypeName == null)
                return;
            ReflectionEventSource.Log.CustomAttributeData_ConstructorArguments(attributeTypeName);
        }

        public static void CustomAttributeData_NamedArguments(CustomAttributeData customAttributeData)
        {
            String attributeTypeName = customAttributeData.AttributeTypeNameString();
            if (attributeTypeName == null)
                return;
            ReflectionEventSource.Log.CustomAttributeData_NamedArguments(attributeTypeName);
        }

        public static void EventInfo_AddMethod(EventInfo eventInfo)
        {
            String declaringTypeName = eventInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String eventName = eventInfo.NameString();
            if (eventName == null)
                return;
            ReflectionEventSource.Log.EventInfo_AddMethod(declaringTypeName, eventName);
        }

        public static void EventInfo_RaiseMethod(EventInfo eventInfo)
        {
            String declaringTypeName = eventInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String eventName = eventInfo.NameString();
            if (eventName == null)
                return;
            ReflectionEventSource.Log.EventInfo_RaiseMethod(declaringTypeName, eventName);
        }

        public static void EventInfo_RemoveMethod(EventInfo eventInfo)
        {
            String declaringTypeName = eventInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String eventName = eventInfo.NameString();
            if (eventName == null)
                return;
            ReflectionEventSource.Log.EventInfo_RemoveMethod(declaringTypeName, eventName);
        }

        public static void EventInfo_CustomAttributes(EventInfo eventInfo)
        {
            String declaringTypeName = eventInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String eventName = eventInfo.NameString();
            if (eventName == null)
                return;
            ReflectionEventSource.Log.EventInfo_CustomAttributes(declaringTypeName, eventName);
        }

        public static void EventInfo_Name(EventInfo eventInfo)
        {
            String declaringTypeName = eventInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String eventName = eventInfo.NameString();
            if (eventName == null)
                return;
            ReflectionEventSource.Log.EventInfo_Name(declaringTypeName, eventName);
        }

        public static void EventInfo_DeclaringType(EventInfo eventInfo)
        {
            String declaringTypeName = eventInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String eventName = eventInfo.NameString();
            if (eventName == null)
                return;
            ReflectionEventSource.Log.EventInfo_DeclaringType(declaringTypeName, eventName);
        }

        public static void FieldInfo_SetValue(FieldInfo fieldInfo, Object obj, Object value)
        {
            String declaringTypeName = fieldInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String fieldName = fieldInfo.NameString();
            if (fieldName == null)
                return;
            ReflectionEventSource.Log.FieldInfo_SetValue(declaringTypeName, fieldName);
        }

        public static void FieldInfo_GetValue(FieldInfo fieldInfo, Object obj)
        {
            String declaringTypeName = fieldInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String fieldName = fieldInfo.NameString();
            if (fieldName == null)
                return;
            ReflectionEventSource.Log.FieldInfo_GetValue(declaringTypeName, fieldName);
        }

        public static void FieldInfo_CustomAttributes(FieldInfo fieldInfo)
        {
            String declaringTypeName = fieldInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String fieldName = fieldInfo.NameString();
            if (fieldName == null)
                return;
            ReflectionEventSource.Log.FieldInfo_CustomAttributes(declaringTypeName, fieldName);
        }

        public static void FieldInfo_Name(FieldInfo fieldInfo)
        {
            String declaringTypeName = fieldInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String fieldName = fieldInfo.NameString();
            if (fieldName == null)
                return;
            ReflectionEventSource.Log.FieldInfo_Name(declaringTypeName, fieldName);
        }

        public static void FieldInfo_DeclaringType(FieldInfo fieldInfo)
        {
            String declaringTypeName = fieldInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String fieldName = fieldInfo.NameString();
            if (fieldName == null)
                return;
            ReflectionEventSource.Log.FieldInfo_DeclaringType(declaringTypeName, fieldName);
        }

        public static void MethodBase_CustomAttributes(MethodBase methodBase)
        {
            String declaringTypeName = methodBase.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodBase.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodBase_CustomAttributes(declaringTypeName, methodName);
        }

        public static void MethodBase_Name(MethodBase methodBase)
        {
            String declaringTypeName = methodBase.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodBase.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodBase_Name(declaringTypeName, methodName);
        }

        public static void MethodBase_DeclaringType(MethodBase methodBase)
        {
            String declaringTypeName = methodBase.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodBase.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodBase_DeclaringType(declaringTypeName, methodName);
        }

        public static void MethodBase_GetParameters(MethodBase methodBase)
        {
            String declaringTypeName = methodBase.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodBase.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodBase_GetParameters(declaringTypeName, methodName);
        }

        public static void ConstructorInfo_Invoke(ConstructorInfo constructor, Object[] parameters)
        {
            String declaringTypeName = constructor.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = constructor.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodBase_Invoke(declaringTypeName, methodName);
        }

        public static void MethodBase_Invoke(MethodBase methodBase, Object obj, Object[] parameters)
        {
            String declaringTypeName = methodBase.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodBase.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodBase_Invoke(declaringTypeName, methodName);
        }

        public static void MethodInfo_ReturnParameter(MethodInfo methodInfo)
        {
            String declaringTypeName = methodInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodInfo.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodInfo_ReturnParameter(declaringTypeName, methodName);
        }

        public static void MethodInfo_ReturnType(MethodInfo methodInfo)
        {
            String declaringTypeName = methodInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodInfo.NameString();
            if (methodName == null)
                return;
            ReflectionEventSource.Log.MethodInfo_ReturnType(declaringTypeName, methodName);
        }

        public static void MethodInfo_MakeGenericMethod(MethodInfo methodInfo, Type[] typeArguments)
        {
            String declaringTypeName = methodInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodInfo.NameString();
            if (methodName == null)
                return;
            String typeArgumentStrings = typeArguments.GenericTypeArgumentStrings();
            if (typeArgumentStrings == null)
                return;
            ReflectionEventSource.Log.MethodInfo_MakeGenericMethod(declaringTypeName, methodName, typeArgumentStrings);
        }

        public static void MethodInfo_CreateDelegate(MethodInfo methodInfo, Type delegateType)
        {
            String declaringTypeName = methodInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodInfo.NameString();
            if (methodName == null)
                return;
            String delegateTypeName = delegateType.NameString();
            if (delegateType == null)
                return;
            ReflectionEventSource.Log.MethodInfo_CreateDelegate(declaringTypeName, methodName, delegateTypeName);
        }

        public static void MethodInfo_CreateDelegate(MethodInfo methodInfo, Type delegateType, Object target)
        {
            String declaringTypeName = methodInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String methodName = methodInfo.NameString();
            if (methodName == null)
                return;
            String delegateTypeName = delegateType.NameString();
            if (delegateType == null)
                return;
            ReflectionEventSource.Log.MethodInfo_CreateDelegate(declaringTypeName, methodName, delegateTypeName);
        }

        public static void PropertyInfo_GetValue(PropertyInfo propertyInfo, Object obj, Object[] index)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_GetValue(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_SetValue(PropertyInfo propertyInfo, Object obj, Object value, Object[] index)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_SetValue(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_GetMethod(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_GetMethod(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_SetMethod(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_SetMethod(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_GetConstantValue(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_GetConstantValue(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_PropertyType(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_PropertyType(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_CustomAttributes(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_CustomAttributes(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_Name(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_Name(declaringTypeName, propertyName);
        }

        public static void PropertyInfo_DeclaringType(PropertyInfo propertyInfo)
        {
            String declaringTypeName = propertyInfo.DeclaringTypeNameString();
            if (declaringTypeName == null)
                return;
            String propertyName = propertyInfo.NameString();
            if (propertyName == null)
                return;
            ReflectionEventSource.Log.PropertyInfo_DeclaringType(declaringTypeName, propertyName);
        }
    }
}

