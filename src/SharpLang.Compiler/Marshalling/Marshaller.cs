using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SharpLang.CompilerServices.Cecil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Base class for marshaller that emit IL code to switch between managed and native representation of objects.
    /// </summary>
    abstract class Marshaller
    {
        private static readonly Dictionary<TypeReference, Marshaller> marshallers = new Dictionary<TypeReference, Marshaller>(MemberEqualityComparer.Default);
        private static readonly Dictionary<TypeReference, bool> blittableValueTypes = new Dictionary<TypeReference, bool>(MemberEqualityComparer.Default);

        /// <summary>
        /// Determines whether the specified type is blittable.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <param name="marshalInfo">The marshal information.</param>
        /// <returns></returns>
        private static bool IsBlittable(TypeReference typeReference, MarshalInfo marshalInfo)
        {
            switch (typeReference.MetadataType)
            {
                case MetadataType.ValueType:
                case MetadataType.Class:
                    return IsValueTypeBlittable(typeReference, marshalInfo);
                case MetadataType.SByte:
                case MetadataType.Byte:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                case MetadataType.Int64:
                case MetadataType.UInt64:
                case MetadataType.Single:
                case MetadataType.Double:
                case MetadataType.IntPtr:
                case MetadataType.UIntPtr:
                case MetadataType.Pointer:
                case MetadataType.Char:
                    return true;
                case MetadataType.Boolean:
                    if (marshalInfo != null)
                    {
                        // I1/U1 boolean need marshalling
                        if (marshalInfo.NativeType == NativeType.I1 || marshalInfo.NativeType == NativeType.U1)
                            return false;
                    }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the specified value type is blittable.
        /// </summary>
        /// <param name="typeReference">The type reference.</param>
        /// <param name="marshalInfo">The marshal information.</param>
        /// <returns></returns>
        private static bool IsValueTypeBlittable(TypeReference typeReference, MarshalInfo marshalInfo)
        {
            bool isBlittable;
            if (blittableValueTypes.TryGetValue(typeReference, out isBlittable))
                return isBlittable;

            var typeDefinition = typeReference.Resolve();

            // Only value types are blittable
            if (typeDefinition.IsValueType)
            {
                isBlittable = true;

                if (!typeDefinition.IsEnum && !typeDefinition.IsExplicitLayout)
                {
                    // Check if every field is blittable
                    foreach (var field in typeDefinition.Fields)
                    {
                        if (field.IsStatic)
                            continue;

                        var fieldType = ResolveGenericsVisitor.Process(typeReference, field.FieldType);
                        if (!IsBlittable(fieldType, field.HasMarshalInfo ? field.MarshalInfo : null))
                        {
                            isBlittable = false;
                            break;
                        }
                    }
                }
            }

            blittableValueTypes[typeReference] = isBlittable;

            return isBlittable;
        }

        /// <summary>
        /// Finds the marshaller for given <see cref="TypeReference"/> and <see cref="MarshalInfo"/>.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="marshalInfo">The marshal information.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException">
        /// </exception>
        public static Marshaller FindMarshallerForType(TypeReference type, MarshalInfo marshalInfo)
        {
            // First, check the cache
            // TODO: Take marshalInfo into account
            Marshaller marshaller;
            if (marshallers.TryGetValue(type, out marshaller))
                return marshaller;

            if (marshalInfo != null)
            {
                switch (marshalInfo.NativeType)
                {
                    case NativeType.IUnknown:
                    case NativeType.IntF:
                        // TODO: Implement a real marshaller for that case
                        return new BlittableMarshaller();
                }
            }

            switch (type.MetadataType)
            {
                case MetadataType.Object:
                case MetadataType.ValueType:
                case MetadataType.Class:
                {
                    // Various types with specific marshallers
                    if (type.FullName == typeof(HandleRef).FullName)
                    {
                        marshaller = new HandleRefMarshaller();
                        break;
                    }

                    if (type.FullName == typeof(StringBuilder).FullName)
                    {
                        marshaller = new StringBuilderMarshaller();
                        break;
                    }

                    if (type.FullName == typeof(string).FullName)
                    {
                        marshaller = new StringMarshaller(marshalInfo);
                        break;
                    }

                    if (type.FullName == typeof(RuntimeTypeHandle).FullName
                        || type.FullName == typeof(RuntimeFieldHandle).FullName
                        || type.FullName == typeof(RuntimeMethodHandle).FullName)
                    {
                        return new BlittableMarshaller();
                    }

                    var typeDefinition = type.Resolve();

                    // Type inherits from SafeHandle?
                    {
                        var currentType = typeDefinition;
                        while (true)
                        {
                            if (currentType.FullName == typeof(SafeHandle).FullName)
                            {
                                return new SafeHandleMarshaller();
                            }
                            if (currentType.BaseType == null)
                                break;
                            currentType = currentType.BaseType.Resolve();
                        }
                    }

                    // Check if type is a delegate
                    if (typeDefinition.BaseType != null && typeDefinition.BaseType.FullName == typeof(MulticastDelegate).FullName)
                    {
                        marshaller = new DelegateMarshaller();
                        break;
                    }

                    // Check if type is a reference type
                    if (!typeDefinition.IsValueType)
                        return new BlittableMarshaller();

                    if (typeDefinition.IsEnum)
                        return new BlittableMarshaller();

                    // Check if type is blittable
                    if (IsBlittable(type, marshalInfo))
                    {
                        marshaller = new BlittableMarshaller();
                        break;
                    }

                    marshaller = new StructMarshaller(type);
                    break;
                }
                case MetadataType.Boolean:
                    if (marshalInfo != null)
                    {
                        // I1/U1 boolean don't need any marshalling
                        if (marshalInfo.NativeType == NativeType.I1 || marshalInfo.NativeType == NativeType.U1)
                            marshaller = new BlittableMarshaller();
                        else
                            throw new NotImplementedException();
                    }
                    else
                    {
                        // Default case: 4-byte integer
                        marshaller = new BooleanMarshaller();
                    }
                    break;
                case MetadataType.SByte:
                case MetadataType.Byte:
                case MetadataType.Int16:
                case MetadataType.UInt16:
                case MetadataType.Int32:
                case MetadataType.UInt32:
                case MetadataType.Int64:
                case MetadataType.UInt64:
                case MetadataType.Single:
                case MetadataType.Double:
                case MetadataType.IntPtr:
                case MetadataType.UIntPtr:
                case MetadataType.Pointer:
                case MetadataType.Char:
                    marshaller = new BlittableMarshaller();
                    break;
                case MetadataType.String:
                    marshaller = new StringMarshaller(marshalInfo);
                    break;
                case MetadataType.Array:
                {
                    var elementType = ((ArrayType)type).ElementType;
                    if (IsBlittable(elementType, marshalInfo))
                    {
                        marshaller = new BlittableArrayMarshaller();
                        break;
                    }

                    marshaller = new ArrayMarshaller(FindMarshallerForType(elementType, marshalInfo));
                    break;
                }
                default:
                    throw new NotImplementedException(string.Format("Marshaller for type {0}", type));
            }

            marshallers[type] = marshaller;
            return marshaller;
        }

        /// <summary>
        /// Determines whether this instance has any backward branches (loop).
        /// If it's the case, its result needs to be stored in a variable and processed earlier so that backward branches have empty stacks.
        /// </summary>
        /// <returns></returns>
        public virtual bool ContainsLoops
        {
            get { return false; }
        }

        /// <summary>
        /// Converts from managed to native and pushes result on the stack.
        /// </summary>
        /// <param name="context">The context.</param>
        public abstract void EmitConvertManagedToNative(MarshalCodeContext context);

        /// <summary>
        /// Converts from native to managed and pushes result on the stack.
        /// </summary>
        /// <param name="context">The context.</param>
        public abstract void EmitConvertNativeToManaged(MarshalCodeContext context);

        /// <summary>
        /// Converts from managed to native and if native stack is not empty, stores result.
        /// </summary>
        /// <param name="context">The context.</param>
        public virtual void EmitStoreManagedToNative(MarshalCodeContext context)
        {
            bool hasNativeEmitter = (context.NativeEmitters.Count > 0);
            if (hasNativeEmitter)
                context.NativeEmitters.Peek().StoreStart(context.ILProcessor);

            EmitConvertManagedToNative(context);

            if (hasNativeEmitter)
                context.NativeEmitters.Peek().StoreEnd(context.ILProcessor);
        }

        /// <summary>
        /// Converts from native to managed and if managed stack is not empty, stores result.
        /// </summary>
        /// <param name="context">The context.</param>
        public virtual void EmitStoreNativeToManaged(MarshalCodeContext context)
        {
            bool hasManagedEmitter = (context.ManagedEmitters.Count > 0);
            if (hasManagedEmitter)
                context.ManagedEmitters.Peek().StoreStart(context.ILProcessor);

            EmitConvertNativeToManaged(context);

            if (hasManagedEmitter)
                context.ManagedEmitters.Peek().StoreEnd(context.ILProcessor);
        }

        /// <summary>
        /// Gets the native type.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        public virtual TypeReference GetNativeType(MarshalCodeContext context)
        {
            return context.ManagedEmitters.Peek().Type;
        }
    }
}