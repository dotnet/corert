// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysisFramework
{
    public abstract class DependencyNode
    {
        private object _mark;

        // Only DependencyNodeCore<T> is allowed to derive from this
        internal DependencyNode()
        { }

        internal void SetMark(object mark)
        {
            Debug.Assert(mark != null);
            Debug.Assert(_mark == null);
            _mark = mark;
        }

        internal object GetMark()
        {
            return _mark;
        }

        public bool Marked
        {
            get
            {
                return _mark != null;
            }
        }

        // Force all non-abstract nodes to provide a name
        public abstract string GetName();

        public sealed override string ToString()
        {
            return GetName();
        }
    }
}
