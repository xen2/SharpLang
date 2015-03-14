using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    struct SharpLangFieldDescription
    {
        // rowid : 24, isStatic: 1
        private uint data1;
        // offset: 27, type: 5
        private uint data2;

        public FieldDefinitionHandle FieldDefinitionHandle
        {
            get
            {
                return FieldDefinitionHandle.FromRowId(data1 & TokenTypeIds.RIDMask);
            }
        }

        public uint Offset
        {
            get { return data2 & 0x7FFFFFF; }
        }

        public CorElementType Type
        {
            get { return (CorElementType)(data2 >> 27); }
        }
    }
}