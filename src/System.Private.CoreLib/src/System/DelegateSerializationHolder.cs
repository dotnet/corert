// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

    
using System.Reflection;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace System
{
    [Serializable]
    public sealed class DelegateSerializationHolder : IObjectReference, ISerializable
    {
        internal static DelegateEntry GetDelegateSerializationInfo(SerializationInfo info, Type delegateType, Object target, MethodInfo method, int targetIndex)
        {
            // Used for MulticastDelegate

            Debug.Assert(method != null);

            Type c = delegateType.BaseType;

            if (c == null || (c != typeof(Delegate) && c != typeof(MulticastDelegate)))
                throw new ArgumentException(SR.Arg_MustBeDelegate);

            if (method.DeclaringType == null)
                throw new NotSupportedException(SR.NotSupported_GlobalMethodSerialization);

            DelegateEntry de = new DelegateEntry(delegateType, target, method);

            if (info.MemberCount == 0)
            {
                info.SetType(typeof(DelegateSerializationHolder));
                info.AddValue("DelegateEntry", de, typeof(DelegateEntry));
            }

            return de;
        }

        [Serializable]
        internal class DelegateEntry
        {
            public Type DelegateType;
            public Object TargetObject;
            public MethodInfo TargetMethod;
            public DelegateEntry NextEntry;

            internal DelegateEntry(Type delegateType, Object target, MethodInfo targetMethod)
            {
                DelegateType = delegateType;
                TargetObject = target;
                TargetMethod = targetMethod;
            }
        }

        private DelegateEntry _delegateEntry;
        private int _delegatesCount;

        public DelegateSerializationHolder(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            Contract.EndContractBlock();

            try
            {
                _delegateEntry = (DelegateEntry)info.GetValue("DelegateEntry", typeof(DelegateEntry));
            }
            catch
            {
                // TODO: If we *really* want to support cross-runtime serialization and deserialization of delegates, 
                // we'll have to implemenent the handling of the old format we have today in CoreCLR.
                // This is not a requirement today.
                _delegateEntry = null;
            }

            _delegatesCount = 0;
            DelegateEntry currentEntry = _delegateEntry;
            while (currentEntry != null)
            {
                _delegatesCount++;
                currentEntry = currentEntry.NextEntry;
            }
        }

        private void ThrowInsufficientState(string field)
        {
            throw new SerializationException(SR.Format(SR.Serialization_InsufficientDeserializationState, field));
        }

        private Delegate GetDelegate(DelegateEntry de)
        {
            try
            {
                if (de.DelegateType == null)
                    ThrowInsufficientState("DelegateType");

                if (de.TargetMethod == null)
                    ThrowInsufficientState("TargetMethod");
                
                return Delegate.CreateDelegate(de.DelegateType, de.TargetObject, de.TargetMethod);
            }
            catch (Exception e) when (!(e is SerializationException))
            {
                throw new SerializationException(e.Message, e);
            }
        }

        public Object GetRealObject(StreamingContext context)
        {
            if (_delegateEntry == null)
                ThrowInsufficientState("DelegateEntry");

            if (_delegatesCount == 1)
            {
                return GetDelegate(_delegateEntry);
            }
            else
            {
                Delegate[] invocationList = new Delegate[_delegatesCount];

                int index = _delegatesCount - 1;
                for (DelegateEntry de = _delegateEntry; de != null; de = de.NextEntry)
                {
                    // Be careful to match the index we pass to GetDelegate (used to look up extra information for each delegate) to
                    // the order we process the entries: we're actually looking at them in reverse order.
                    invocationList[index--] = GetDelegate(de);
                }
                return ((MulticastDelegate)invocationList[0]).NewMulticastDelegate(invocationList, invocationList.Length);
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            throw new NotSupportedException(SR.NotSupported_DelegateSerHolderSerial);
        }
    }
}