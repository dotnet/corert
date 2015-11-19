// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Internal.TypeSystem;

namespace ILCompiler
{
    public enum ReadyToRunHelperId
    {
        NewHelper,
        NewArr1,
        VirtualCall,
        IsInstanceOf,
        CastClass,
        GetNonGCStaticBase,
        GetGCStaticBase,
        GetThreadStaticBase,
        DelegateCtor,
    }

    public class ReadyToRunHelper : IEquatable<ReadyToRunHelper>
    {
        private Compilation _compilation;

        public ReadyToRunHelper(Compilation compilation, ReadyToRunHelperId id, Object target)
        {
            _compilation = compilation;

            this.Id = id;
            this.Target = target;
        }

        public ReadyToRunHelperId Id { get; private set; }
        public Object Target { get; private set; }

        public string MangledName
        {
            get
            {
                switch (this.Id)
                {
                    case ReadyToRunHelperId.NewHelper:
                        return "__NewHelper_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.NewArr1:
                        return "__NewArr1_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.VirtualCall:
                        return "__VirtualCall_" + _compilation.NameMangler.GetMangledMethodName((MethodDesc)this.Target);
                    case ReadyToRunHelperId.IsInstanceOf:
                        return "__IsInstanceOf_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.CastClass:
                        return "__CastClass_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.GetNonGCStaticBase:
                        return "__GetNonGCStaticBase_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.GetGCStaticBase:
                        return "__GetGCStaticBase_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.GetThreadStaticBase:
                        return "__GetThreadStaticBase_" + _compilation.NameMangler.GetMangledTypeName((TypeDesc)this.Target);
                    case ReadyToRunHelperId.DelegateCtor:
                        return "__DelegateCtor_" + _compilation.NameMangler.GetMangledMethodName(((DelegateInfo)this.Target).Target);
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public bool Equals(ReadyToRunHelper other)
        {
            return (Id == other.Id) && ReferenceEquals(Target, other.Target);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Target.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ReadyToRunHelper))
                return false;

            return Equals((ReadyToRunHelper)obj);
        }
    }
}
