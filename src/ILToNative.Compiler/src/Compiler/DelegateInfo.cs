using Internal.IL.Stubs;
using Internal.TypeSystem;

namespace ILToNative
{
    class DelegateInfo
    {
        public DelegateInfo(MethodDesc target, MethodDesc ctor, DelegateShuffleThunk shuffleThunk)
        {
            this.Target = target;
            this.Ctor = ctor;
            this.ShuffleThunk = shuffleThunk;
        }

        public MethodDesc Target { get; private set; }
        public MethodDesc Ctor { get; private set; }
        public DelegateShuffleThunk ShuffleThunk { get; private set; }

    }
}
