// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.Tracing;

namespace System.Reflection.Runtime.Tracing
{
    [EventSource(Guid = "55B578AE-32B0-48F8-822F-B3245E6FA59C", Name = "System.Reflection.Runtime.Tracing")]
    internal sealed class ReflectionEventSource : EventSource
    {
        // Defines the singleton instance for the Resources ETW provider
        public static readonly ReflectionEventSource Log = new ReflectionEventSource();

        public static bool IsInitialized
        {
            get
            {
                return Log != null;
            }
        }

        private ReflectionEventSource() { }


        #region Reflection Event Handlers
        [Event(1)]
        public void TypeInfo_CustomAttributes(String typeName)
        {
            WriteEvent(1, typeName);
        }

        [Event(2)]
        public void TypeInfo_Name(String typeName)
        {
            WriteEvent(2, typeName);
        }

        [Event(3)]
        public void TypeInfo_BaseType(String typeName)
        {
            WriteEvent(3, typeName);
        }

        [Event(4)]
        public void TypeInfo_DeclaredConstructors(String typeName)
        {
            WriteEvent(4, typeName);
        }

        [Event(5)]
        public void TypeInfo_DeclaredEvents(String typeName)
        {
            WriteEvent(5, typeName);
        }

        [Event(6)]
        public void TypeInfo_DeclaredFields(String typeName)
        {
            WriteEvent(6, typeName);
        }

        [Event(7)]
        public void TypeInfo_DeclaredMembers(String typeName)
        {
            WriteEvent(7, typeName);
        }

        [Event(8)]
        public void TypeInfo_DeclaredMethods(String typeName)
        {
            WriteEvent(8, typeName);
        }

        [Event(9)]
        public void TypeInfo_DeclaredNestedTypes(String typeName)
        {
            WriteEvent(9, typeName);
        }

        [Event(10)]
        public void TypeInfo_DeclaredProperties(String typeName)
        {
            WriteEvent(10, typeName);
        }

        [Event(11)]
        public void TypeInfo_DeclaringMethod(String typeName)
        {
            WriteEvent(11, typeName);
        }

        [Event(12)]
        public void TypeInfo_FullName(String typeName)
        {
            WriteEvent(12, typeName);
        }

        [Event(13)]
        public void TypeInfo_Namespace(String typeName)
        {
            WriteEvent(13, typeName);
        }

        [Event(14)]
        public void TypeInfo_GetDeclaredEvent(String typeName, String eventName)
        {
            WriteEvent(14, typeName, eventName);
        }

        [Event(15)]
        public void TypeInfo_GetDeclaredField(String typeName, String fieldName)
        {
            WriteEvent(15, typeName, fieldName);
        }

        [Event(16)]
        public void TypeInfo_GetDeclaredMethod(String typeName, String methodName)
        {
            WriteEvent(16, typeName, methodName);
        }

        [Event(17)]
        public void TypeInfo_GetDeclaredProperty(String typeName, String propertyName)
        {
            WriteEvent(17, typeName, propertyName);
        }

        [Event(18)]
        public void TypeInfo_MakeArrayType(String typeName)
        {
            WriteEvent(18, typeName);
        }

        [Event(19)]
        public void TypeInfo_MakeByRefType(String typeName)
        {
            WriteEvent(19, typeName);
        }

        [Event(20)]
        public void TypeInfo_MakeGenericType(String typeName, String typeArguments)
        {
            WriteEvent(20, typeName, typeArguments);
        }

        [Event(21)]
        public void TypeInfo_MakePointerType(String typeName)
        {
            WriteEvent(21, typeName);
        }

        [Event(22)]
        public void Assembly_DefinedTypes(String assemblyName)
        {
            WriteEvent(22, assemblyName);
        }

        [Event(23)]
        public void Assembly_GetType(String assemblyName, String typeName)
        {
            WriteEvent(23, assemblyName, typeName);
        }

        [Event(24)]
        public void Assembly_CustomAttributes(String assemblyName)
        {
            WriteEvent(24, assemblyName);
        }

        [Event(25)]
        public void Assembly_FullName(String assemblyName)
        {
            WriteEvent(25, assemblyName);
        }

        [Event(26)]
        public void Assembly_GetName(String assemblyName)
        {
            WriteEvent(26, assemblyName);
        }

        [Event(27)]
        public void CustomAttributeData_ConstructorArguments(String caName)
        {
            WriteEvent(27, caName);
        }

        [Event(28)]
        public void CustomAttributeData_NamedArguments(String caName)
        {
            WriteEvent(28, caName);
        }

        [Event(29)]
        public void EventInfo_AddMethod(String typeName, String eventName)
        {
            WriteEvent(29, typeName, eventName);
        }

        [Event(30)]
        public void EventInfo_RaiseMethod(String typeName, String eventName)
        {
            WriteEvent(30, typeName, eventName);
        }

        [Event(31)]
        public void EventInfo_RemoveMethod(String typeName, String eventName)
        {
            WriteEvent(31, typeName, eventName);
        }

        [Event(32)]
        public void EventInfo_CustomAttributes(String typeName, String eventName)
        {
            WriteEvent(32, typeName, eventName);
        }

        [Event(33)]
        public void EventInfo_Name(String typeName, String eventName)
        {
            WriteEvent(33, typeName, eventName);
        }

        [Event(34)]
        public void EventInfo_DeclaringType(String typeName, String eventName)
        {
            WriteEvent(34, typeName, eventName);
        }

        [Event(35)]
        public void FieldInfo_SetValue(String typeName, String fieldName)
        {
            WriteEvent(35, typeName, fieldName);
        }

        [Event(36)]
        public void FieldInfo_GetValue(String typeName, String fieldName)
        {
            WriteEvent(36, typeName, fieldName);
        }

        [Event(37)]
        public void FieldInfo_CustomAttributes(String typeName, String fieldName)
        {
            WriteEvent(37, typeName, fieldName);
        }

        [Event(38)]
        public void FieldInfo_Name(String typeName, String fieldName)
        {
            WriteEvent(38, typeName, fieldName);
        }

        [Event(39)]
        public void FieldInfo_DeclaringType(String typeName, String fieldName)
        {
            WriteEvent(39, typeName, fieldName);
        }

        [Event(40)]
        public void MethodBase_CustomAttributes(String typeName, String methodName)
        {
            WriteEvent(40, typeName, methodName);
        }

        [Event(41)]
        public void MethodBase_Name(String typeName, String methodName)
        {
            WriteEvent(41, typeName, methodName);
        }

        [Event(42)]
        public void MethodBase_DeclaringType(String typeName, String methodName)
        {
            WriteEvent(42, typeName, methodName);
        }

        [Event(43)]
        public void MethodBase_GetParameters(String typeName, String methodName)
        {
            WriteEvent(43, typeName, methodName);
        }

        [Event(44)]
        public void MethodBase_Invoke(String typeName, String methodName)
        {
            WriteEvent(44, typeName, methodName);
        }

        [Event(45)]
        public void MethodInfo_ReturnParameter(String typeName, String methodName)
        {
            WriteEvent(45, typeName, methodName);
        }

        [Event(46)]
        public void MethodInfo_ReturnType(String typeName, String methodName)
        {
            WriteEvent(46, typeName, methodName);
        }

        [Event(47)]
        public void MethodInfo_MakeGenericMethod(String typeName, String methodName, String typeArguments)
        {
            WriteEvent(47, typeName, methodName, typeArguments);
        }

        [Event(48)]
        public void MethodInfo_CreateDelegate(String typeName, String methodName, String delegateTypeName)
        {
            WriteEvent(48, typeName, methodName, delegateTypeName);
        }

        [Event(49)]
        public void PropertyInfo_GetValue(String typeName, String propertyName)
        {
            WriteEvent(49, typeName, propertyName);
        }

        [Event(50)]
        public void PropertyInfo_SetValue(String typeName, String propertyName)
        {
            WriteEvent(50, typeName, propertyName);
        }

        [Event(51)]
        public void PropertyInfo_GetMethod(String typeName, String propertyName)
        {
            WriteEvent(51, typeName, propertyName);
        }

        [Event(52)]
        public void PropertyInfo_SetMethod(String typeName, String propertyName)
        {
            WriteEvent(52, typeName, propertyName);
        }

        [Event(53)]
        public void PropertyInfo_GetConstantValue(String typeName, String propertyName)
        {
            WriteEvent(53, typeName, propertyName);
        }

        [Event(54)]
        public void PropertyInfo_PropertyType(String typeName, String propertyName)
        {
            WriteEvent(54, typeName, propertyName);
        }

        [Event(55)]
        public void PropertyInfo_CustomAttributes(String typeName, String propertyName)
        {
            WriteEvent(55, typeName, propertyName);
        }

        [Event(56)]
        public void PropertyInfo_Name(String typeName, String propertyName)
        {
            WriteEvent(56, typeName, propertyName);
        }

        [Event(57)]
        public void PropertyInfo_DeclaringType(String typeName, String propertyName)
        {
            WriteEvent(57, typeName, propertyName);
        }

        [Event(58)]
        public void TypeInfo_AssemblyQualifiedName(String typeName)
        {
            WriteEvent(58, typeName);
        }
        #endregion
    }
}
