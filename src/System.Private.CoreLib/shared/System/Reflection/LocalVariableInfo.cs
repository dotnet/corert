// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace System.Reflection
{
    public class LocalVariableInfo
    {
#if CORECLR
        private RuntimeType _type;
#endif
        private bool _isPinned = false;
        private int _localIndex = 0;

        public virtual Type LocalType
        { 
            get 
            {
                Debug.Fail("type must be set!");
#if CORECLR
                return _type;
#else
                return null;
#endif                
            }
        }
        
        public virtual bool IsPinned => _isPinned;
        public virtual int LocalIndex => _localIndex;

        protected LocalVariableInfo() { }
        
        public override string ToString()
        {
            // This is really how the desktop behaves if you don't override, including the NullReference when 
            // it calls ToString() on LocalType's null return.
            string toString = LocalType.ToString() + " (" + LocalIndex + ")";

            if (IsPinned)
                toString += " (pinned)";

            return toString;
        }
    }
}

