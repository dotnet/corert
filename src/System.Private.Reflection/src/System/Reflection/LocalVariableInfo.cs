// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  LocalVariableInfo
**
==============================================================*/

using global::System;
using global::System.Diagnostics.Contracts;

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
                Contract.Assert(false, "type must be set!");
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

