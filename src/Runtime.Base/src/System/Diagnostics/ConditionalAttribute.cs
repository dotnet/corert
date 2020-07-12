// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Diagnostics
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    internal sealed class ConditionalAttribute : Attribute
    {
        public ConditionalAttribute(String conditionString)
        {
            _conditionString = conditionString;
        }

        public String ConditionString
        {
            get
            {
                return _conditionString;
            }
        }

        private String _conditionString;
    }
}
