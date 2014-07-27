namespace SharpLLVM
{
    public partial struct ValueRef
    {
        public static readonly ValueRef Empty = new ValueRef();

        public override string ToString()
        {
            return LLVM.PrintValueToString(this);
        }
    }
}