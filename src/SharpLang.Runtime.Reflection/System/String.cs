using System.Text;

namespace System
{
    partial class String
    {
        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe String CtorSBytePtr(sbyte* value)
        {
            if (value != null)
            {
                sbyte* current = value;
                int length = 0;
                while (*current++ != 0)
                    length++;

                return CreateString(value, 0, length, null);
            }
            else
                return String.Empty;
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe String CtorSBytePtr(sbyte* value, int startIndex, int length)
        {
            if (value != null)
                return CreateString(value, startIndex, length, null);
            else
                return String.Empty;
        }

        [System.Security.SecuritySafeCritical]  // auto-generated
        private unsafe String CtorSBytePtr(sbyte* value, int startIndex, int length, Encoding enc)
        {
            if (value != null)
                return CreateString(value, startIndex, length, enc);
            else
                return String.Empty;
        }
    }
}