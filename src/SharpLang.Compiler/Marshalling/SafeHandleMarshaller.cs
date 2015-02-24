// Copyright (c) 2014 SharpLang - Virgile Bello

using System;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SharpLang.CompilerServices.Marshalling
{
    /// <summary>
    /// Marshaller for <see cref="SafeHandle"/>.
    /// </summary>
    class SafeHandleMarshaller : Marshaller
    {
        public override void EmitConvertManagedToNative(MarshalCodeContext context)
        {
            var corlib = context.Assembly.MainModule.Import(typeof(void)).Resolve().Module.Assembly;
            var safeHandle = corlib.MainModule.GetType(typeof(SafeHandle).FullName);

            context.ManagedEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Ldfld, context.Assembly.MainModule.Import(safeHandle.Fields.First(x => x.Name == "handle")));
        }

        public override void EmitConvertNativeToManaged(MarshalCodeContext context)
        {
            // TODO: Not implemented yet
            //context.NativeEmitters.Peek().Emit(context.ILProcessor);
            context.ILProcessor.Emit(OpCodes.Ldnull);
        }

        public override TypeReference GetNativeType(MarshalCodeContext context)
        {
            return context.Assembly.MainModule.Import(typeof(IntPtr));
        }
    }
}