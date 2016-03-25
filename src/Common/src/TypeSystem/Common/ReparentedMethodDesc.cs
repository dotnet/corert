namespace Internal.TypeSystem
{
    public class ReparentedMethodDesc : MethodDesc
    {
        private MethodDesc _shadowMethodDesc;
        private TypeDesc _owningType;

        public MethodDesc ShadowMethod
        {
            get
            {
                return _shadowMethodDesc;
            }
        }

        public ReparentedMethodDesc(TypeDesc owningType, MethodDesc shadowMethodDesc)
        {
            _owningType = owningType;
            _shadowMethodDesc = shadowMethodDesc;
        }


        public override TypeSystemContext Context
        {
            get
            {
                return _shadowMethodDesc.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _owningType;
            }
        }

        public override MethodSignature Signature
        {
            get
            {
                return _shadowMethodDesc.Signature;
            }
        }

        public override bool IsVirtual
        {
            get
            {
                return _shadowMethodDesc.IsVirtual;
            }
        }

        public override bool IsNewSlot
        {
            get
            {
                return _shadowMethodDesc.IsNewSlot;
            }
        }

        public override bool IsFinal
        {
            get
            {
                return _shadowMethodDesc.IsFinal;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return _shadowMethodDesc.IsNoInlining;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return _shadowMethodDesc.IsAggressiveInlining;
            }
        }

        public override bool IsRuntimeImplemented
        {
            get
            {
                return _shadowMethodDesc.IsRuntimeImplemented;
            }
        }

        public override bool IsIntrinsic
        {
            get
            {
                return _shadowMethodDesc.IsIntrinsic;
            }
        }

        public override string Name
        {
            get
            {
                return _shadowMethodDesc.Name;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                return _shadowMethodDesc.Instantiation;
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return _shadowMethodDesc.HasCustomAttribute(attributeNamespace, attributeName);
        }

        public override string ToString()
        {
            return _owningType.ToString() + "." + Name;
        }

        public override bool IsPInvoke
        {
            get
            {
                return _shadowMethodDesc.IsPInvoke;
            }
        }

        public override PInvokeMetadata GetPInvokeMethodMetadata()
        {
            return _shadowMethodDesc.GetPInvokeMethodMetadata();
        }
    }
}
