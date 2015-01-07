namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Defines an ABI (how to pass parameters to function calls).
    /// </summary>
    interface IABI
    {
        /// <summary>
        /// Describe how a function parameter of given type is to be passed.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        ABIParameterInfo GetParameterInfo(Type type);
    }
}