using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Context for marshaling: object being marshalled, assembly, IL processor, etc...
    /// </summary>
    class MarshalCodeContext
    {
        private readonly MarshalledObjectEmitterStack managedEmitters = new MarshalledObjectEmitterStack();
        private readonly MarshalledObjectEmitterStack nativeEmitters = new MarshalledObjectEmitterStack();

        public MarshalledObjectEmitterStack ManagedEmitters
        {
            get { return managedEmitters; }
        }

        public MarshalledObjectEmitterStack NativeEmitters
        {
            get { return nativeEmitters; }
        }

        public MarshalCodeContext(AssemblyDefinition assemblyDefinition, MethodDefinition method, bool isCleanupInline)
        {
            Assembly = assemblyDefinition;
            Method = method;
            ILProcessor = method.Body.GetILProcessor();
            IsCleanupInlined = isCleanupInline;
        }

        /// <summary>
        /// Gets a value indicating whether cleanup will be done in same method (if yes, fixed pattern can be used).
        /// </summary>
        /// <value>
        ///   <c>true</c> if [cleanup is inlined]; otherwise, <c>false</c>.
        /// </value>
        public bool IsCleanupInlined { get; private set; }

        public AssemblyDefinition Assembly { get; private set; }

        public MethodDefinition Method { get; private set; }

        public ILProcessor ILProcessor { get; private set; }
    }
}