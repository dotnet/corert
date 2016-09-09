// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  LocalVariableInfo
**
==============================================================*/

using System.Diagnostics;

namespace System.Reflection
{
    public class LocalVariableInfo
    {
        protected LocalVariableInfo()
        {
        }

        public virtual bool IsPinned
        {
            get
            {
                return false;
            }
        }

        public virtual int LocalIndex
        {
            get
            {
                return 0;
            }
        }

        public virtual Type LocalType
        {
            get
            {
                // Don't laugh - this is really how the desktop behaves if you don't override.
                Debug.Assert(false, "type must be set!");
                return null;
            }
        }

        public override String ToString()
        {
            // Don't laugh - this is really how the desktop behaves if you don't override, including the NullReference when 
            // it calls ToString() on LocalType's null return.
            String toString = LocalType.ToString() + " (" + LocalIndex + ")";

            if (IsPinned)
                toString += " (pinned)";

            return toString;
        }
    }
}

