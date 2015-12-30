// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

using System;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    // This attribute is applied on the return value to specify the name of the return value.
    // In WindowsRuntime all parameters including return value need to have unique names.
    // This is essential in JS as one of the ways to get at the results of a method in JavaScript is via a Dictionary object keyed by parameter name.
    [AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Delegate, AllowMultiple = false, Inherited = false)]
    public sealed class ReturnValueNameAttribute : Attribute
    {
        private string m_Name;
        public ReturnValueNameAttribute(string name)
        {
            m_Name = name;
        }

        public string Name
        {
            get { return m_Name; }
        }
    }
}
