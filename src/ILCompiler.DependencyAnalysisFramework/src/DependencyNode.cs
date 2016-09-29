// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        protected internal abstract string GetName();

        public override string ToString()
        {
            return GetName();
        }

        public sealed override bool Equals(object obj)
        {
            return Object.ReferenceEquals(this, obj);
        }

        public sealed override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
