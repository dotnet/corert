// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
**  An attribute to suppress violation messages/warnings   
**  by static code analysis tools. 
**
** 
===========================================================*/

using System;

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(
     AttributeTargets.All,
     Inherited = false,
     AllowMultiple = true
     )
    ]
    [Conditional("CODE_ANALYSIS")]
    public sealed class SuppressMessageAttribute : Attribute
    {
        private string _category;
        private string _justification;
        private string _checkId;
        private string _scope;
        private string _target;
        private string _messageId;

        public SuppressMessageAttribute(string category, string checkId)
        {
            _category = category;
            _checkId = checkId;
        }

        public string Category
        {
            get { return _category; }
        }

        public string CheckId
        {
            get { return _checkId; }
        }

        public string Scope
        {
            get { return _scope; }
            set { _scope = value; }
        }

        public string Target
        {
            get { return _target; }
            set { _target = value; }
        }

        public string MessageId
        {
            get { return _messageId; }
            set { _messageId = value; }
        }

        public string Justification
        {
            get { return _justification; }
            set { _justification = value; }
        }
    }
}
