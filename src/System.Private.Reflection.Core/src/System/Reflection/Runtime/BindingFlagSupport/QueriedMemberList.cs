// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    //
    // Stores the result of a stage 1 member filtering (filtering by name and visibility from the reflected type.) May be cached long term.
    // Allows copy-minimizing but unsafe access - use caution.
    //
    internal sealed class QueriedMemberList<M> where M : MemberInfo
    {
        public QueriedMemberList()
        {
            _members = new M[Grow];
            _allFlagsThatMustMatch = new BindingFlags[Grow];
        }

        public void Add(M member, BindingFlags allFlagsThatMustMatch)
        {
            const BindingFlags validBits = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            Debug.Assert((allFlagsThatMustMatch & ~validBits) == 0);
            Debug.Assert(((allFlagsThatMustMatch & BindingFlags.Public) == 0) != ((allFlagsThatMustMatch & BindingFlags.NonPublic) == 0));
            Debug.Assert(((allFlagsThatMustMatch & BindingFlags.Instance) == 0) != ((allFlagsThatMustMatch & BindingFlags.Static) == 0));
            Debug.Assert((allFlagsThatMustMatch & BindingFlags.FlattenHierarchy) == 0 || (allFlagsThatMustMatch & BindingFlags.Static) != 0);

            int count = _count;
            if (count == _members.Length)
            {
                Array.Resize(ref _members, count + Grow);
                Array.Resize(ref _allFlagsThatMustMatch, count + Grow);
            }

            _members[count] = member;
            _allFlagsThatMustMatch[count] = allFlagsThatMustMatch;
            _count++;
        }

        public int Count => _count;

        /// <summary>
        /// Caution: Will have extra null entries - use QueriedMember.Count to stop iterating rather than MembersNoCopy.Length. Contents are invalidated if an Add occurs.
        /// </summary>
        public M[] MembersNoCopy => _members;

        /// <summary>
        /// Caution: Will have extra null entries - use QueriedMember.Count to stop iterating rather than MembersNoCopy.Length. Contents are invalidated if an Add occurs.
        /// </summary>
        public BindingFlags[] AllFlagsThatMustMatchNoCopy => _allFlagsThatMustMatch;

        private int _count;
        private M[] _members;
        private BindingFlags[] _allFlagsThatMustMatch;

        private const int Grow = 64;
    }
}

