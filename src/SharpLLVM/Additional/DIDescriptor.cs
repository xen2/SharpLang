// Copyright (c) 2014 SharpLang - Virgile Bello
namespace SharpLLVM
{
    public partial struct DIDescriptor
    {
        public static readonly DIDescriptor Empty = new DIDescriptor();

        public override string ToString()
        {
            return LLVM.DIPrintDescriptorToString(this);
        }
    }
}