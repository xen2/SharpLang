using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    struct SharpLangEETypeDefinition : IEquatable<SharpLangEETypeDefinition>
    {
        public IntPtr ModulePointer;
        public TypeDefinitionHandle Handle;

        public SharpLangEETypeDefinition(SharpLangModule module, TypeDefinitionHandle handle) : this()
        {
            Module = module;
            Handle = handle;
        }

        public unsafe SharpLangModule Module
        {
            get { return (SharpLangModule)SharpLangHelper.GetObjectFromPointer((void*)ModulePointer); }
            set { ModulePointer = (IntPtr)SharpLangHelper.GetObjectPointer(value); }
        }

        public bool Equals(SharpLangEETypeDefinition other)
        {
            return ModulePointer.Equals(other.ModulePointer) && Handle.Equals(other.Handle);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SharpLangEETypeDefinition && Equals((SharpLangEETypeDefinition)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ModulePointer.GetHashCode()*397) ^ Handle.GetHashCode();
            }
        }
    }
}