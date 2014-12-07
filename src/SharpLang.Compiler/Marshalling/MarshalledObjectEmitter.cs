using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Emits IL code to access or set being currently marshalled.
    /// </summary>
    abstract class MarshalledObjectEmitter
    {
        public MarshalledObjectEmitter Previous { get; set; }

        public abstract TypeReference Type { get; }

        /// <summary>
        /// Emits IL code to have current object on the IL stack.
        /// </summary>
        /// <param name="ilProcessor">The IL processor.</param>
        public abstract void Emit(ILProcessor ilProcessor);

        /// <summary>
        /// Emits IL code to have current object address on the IL stack.
        /// </summary>
        /// <param name="ilProcessor">The IL processor.</param>
        public abstract void EmitAddress(ILProcessor ilProcessor);

        /// <summary>
        /// Emits IL code that should happen before pushing object to store on the stack.
        /// </summary>
        /// <param name="ilProcessor">The IL processor.</param>
        public virtual void StoreStart(ILProcessor ilProcessor)
        {
            
        }

        /// <summary>
        /// Emits IL code to store object from IL stack to current object.
        /// </summary>
        /// <param name="ilProcessor">The IL processor.</param>
        public abstract void StoreEnd(ILProcessor ilProcessor);
    }
}