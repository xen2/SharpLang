namespace SharpLang.CompilerServices
{
    public sealed partial class Compiler
    {
        private bool charUsesUtf8 = false;
        private bool stringSliceable = false;

        /// <summary>
        /// Gets or sets a value indicating whether char and string types uses UTF8 or UTF16.
        /// </summary>
        /// <value>
        ///   <c>true</c> if char and string types uses UTF8 or UTF16; otherwise, <c>false</c>.
        /// </value>
        public bool CharUsesUTF8
        {
            get { return charUsesUtf8; }
            set { charUsesUtf8 = value; }
        }

        public bool StringSliceable
        {
            get { return stringSliceable; }
            set { stringSliceable = value; }
        }
    }
}