// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;

namespace Internal.Reflection.Extensibility
{
    public abstract class ExtensibleAssembly : Assembly
    {
        protected ExtensibleAssembly()
        {
        }
    }

    public abstract class ExtensibleConstructorInfo : ConstructorInfo
    {
        protected ExtensibleConstructorInfo()
        {
        }
    }
    
    public abstract class ExtensibleCustomAttributeData : CustomAttributeData
    {
        protected ExtensibleCustomAttributeData()
        {
        }
        
        public static CustomAttributeNamedArgument CreateCustomAttributeNamedArgument(System.Type attributeType, string memberName, bool isField, CustomAttributeTypedArgument typedValue) 
        { 
            return new CustomAttributeNamedArgument(attributeType, memberName, isField, typedValue);
        }
        
        public static CustomAttributeTypedArgument CreateCustomAttributeTypedArgument(System.Type argumentType, object value)
        {
            return new CustomAttributeTypedArgument(argumentType, value);
        }
    }
    
    public abstract class ExtensibleEventInfo : EventInfo
    {
        protected ExtensibleEventInfo()
        {
        }
    }
    
    public abstract class ExtensibleFieldInfo : FieldInfo
    {
        protected ExtensibleFieldInfo()
        {
        }
    }
    
    public abstract class ExtensibleMethodInfo : MethodInfo
    {
        protected ExtensibleMethodInfo()
        {
        }
    }
    
    public abstract class ExtensibleModule : Module
    {
        protected ExtensibleModule()
        {
        }
    }
    
    public abstract class ExtensibleParameterInfo : ParameterInfo
    {
        protected ExtensibleParameterInfo()
        {
        }
    }
    
    public abstract class ExtensiblePropertyInfo : PropertyInfo 
    {
        protected ExtensiblePropertyInfo()
        {
        }
    }
    
    public abstract class ExtensibleTypeInfo : TypeInfo
    {
        protected ExtensibleTypeInfo()
        {
        }
    }
}