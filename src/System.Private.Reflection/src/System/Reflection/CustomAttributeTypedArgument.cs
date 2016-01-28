// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  CustomAttributeTypedArgument
**
==============================================================*/

using global::System;
using global::System.Diagnostics.Contracts;

namespace System.Reflection
{
    public struct CustomAttributeTypedArgument
    {
        public CustomAttributeTypedArgument(Type argumentType, Object value)
        {
            // value can be null.
            if (argumentType == null)
                throw new ArgumentNullException("argumentType");

            m_value = (value == null) ? null : CanonicalizeValue(value);
            m_argumentType = argumentType;
        }

        public Type ArgumentType
        {
            get
            {
                return m_argumentType;
            }
        }

        public Object Value
        {
            get
            {
                return m_value;
            }
        }

        private static Object CanonicalizeValue(Object value)
        {
            Contract.Assert(value != null);

            if (value is Enum)
            {
                // Blech.
                TypeInfo enumTypeInfo = value.GetType().GetTypeInfo();
                FieldInfo valueField = enumTypeInfo.GetDeclaredField("value__");
                value = valueField.GetValue(value);
            }
            return value;
        }


        private Object m_value;
        private Type m_argumentType;
    }
}


