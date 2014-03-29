using System;
using NUnit.Framework;

namespace SharpLLVM.Tests
{
    [TestFixture]
    public class TestSharpLLVM
    {
        [Test]
        public void ModuleCreate()
        {
            var module = LLVM.ModuleCreateWithName("Module Name");
            Assert.That(module.Value, Is.Not.EqualTo(IntPtr.Zero));
            LLVM.DisposeModule(module);
        }
    }
}