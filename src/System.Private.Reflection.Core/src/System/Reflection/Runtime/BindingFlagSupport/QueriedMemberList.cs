// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

namespace System.Reflection.Runtime.BindingFlagSupport
{
    //
    // Stores the result of a member filtering that's filtered by name and visibility from base class (as defined by the Type.Get*() family of apis).
    // This object is a good candidate for long term caching.
    //
    internal sealed class QueriedMemberList<M> where M : MemberInfo
    {
        private QueriedMemberList()
        {
            _members = new M[Grow];
            _allFlagsThatMustMatch = new BindingFlags[Grow];
        }

        private QueriedMemberList(int count, M[] members, BindingFlags[] allFlagsThatMustMatch)
        {
            _count = count;
            _members = members;
            _allFlagsThatMustMatch = allFlagsThatMustMatch;
        }

        public int Count => _count;

        public M this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(index >= 0 && index < _count);
                return _members[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Matches(int index, BindingFlags bindingAttr)
        {
            Debug.Assert(index >= 0 && index < Count);
            BindingFlags allFlagsThatMustMatch = _allFlagsThatMustMatch[index];
            return ((bindingAttr & allFlagsThatMustMatch) == allFlagsThatMustMatch);
        }

        public QueriedMemberList<M> Filter(Func<M, bool> predicate)
        {
            BindingFlags[] newAllFlagsThatMustMatch = new BindingFlags[_count];
            M[] newMembers = new M[_count];
            int newCount = 0;
            for (int i = 0; i < _count; i++)
            {
                M member = _members[i];
                if (predicate(member))
                {
                    newMembers[newCount] = member;
                    newAllFlagsThatMustMatch[newCount] = _allFlagsThatMustMatch[i];
                    newCount++;
                }
            }

            return new QueriedMemberList<M>(newCount, newMembers, newAllFlagsThatMustMatch);
        }

        //
        // Filter by name and visibility from the ReflectedType.
        //
        public static QueriedMemberList<M> Create(RuntimeTypeInfo type, string optionalNameFilter, bool ignoreCase, bool declaredOnly)
        {
            RuntimeTypeInfo reflectedType = type;

            MemberPolicies<M> policies = MemberPolicies<M>.Default;

            NameFilter nameFilter;
            if (optionalNameFilter == null)
                nameFilter = null;
            else if (ignoreCase)
                nameFilter = new NameFilterCaseInsensitive(optionalNameFilter);
            else
                nameFilter = new NameFilterCaseSensitive(optionalNameFilter);

            bool inBaseClass = false;
            QueriedMemberList<M> queriedMembers = new QueriedMemberList<M>();
            while (type != null)
            {
                int numCandidatesInDerivedTypes = queriedMembers.Count;

                foreach (M member in policies.CoreGetDeclaredMembers(type, nameFilter, reflectedType))
                {
                    MethodAttributes visibility;
                    bool isStatic;
                    bool isVirtual;
                    bool isNewSlot;
                    policies.GetMemberAttributes(member, out visibility, out isStatic, out isVirtual, out isNewSlot);

                    if (inBaseClass && visibility == MethodAttributes.Private)
                        continue;

                    if (numCandidatesInDerivedTypes != 0 && policies.IsSuppressedByMoreDerivedMember(member, queriedMembers._members, startIndex: 0, endIndex: numCandidatesInDerivedTypes))
                        continue;

                    BindingFlags allFlagsThatMustMatch = default(BindingFlags);
                    allFlagsThatMustMatch |= (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                    if (isStatic && inBaseClass)
                        allFlagsThatMustMatch |= BindingFlags.FlattenHierarchy;
                    allFlagsThatMustMatch |= ((visibility == MethodAttributes.Public) ? BindingFlags.Public : BindingFlags.NonPublic);

                    if (inBaseClass)
                    {
                        queriedMembers.Add(policies.GetInheritedMemberInfo(member, reflectedType), allFlagsThatMustMatch);
                    }
                    else
                    {
                        queriedMembers.Add(member, allFlagsThatMustMatch);
                    }
                }

                if (declaredOnly)
                    break;

                inBaseClass = true;
                type = type.BaseType.CastToRuntimeTypeInfo();
            }

            return queriedMembers;
        }

        private void Add(M member, BindingFlags allFlagsThatMustMatch)
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

        private int _count;
        private M[] _members;
        private BindingFlags[] _allFlagsThatMustMatch;

        private const int Grow = 64;
    }
}

