namespace SharpLLVM
{
    public partial struct ValueRef
    {
        public override string ToString()
        {
            return LLVM.PrintValueToString(this);
        }
    }
}