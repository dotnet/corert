// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Diagnostics
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class ConditionalAttribute : Attribute
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
