using System.Runtime.InteropServices;

public static class Program
{
    [StructLayout(LayoutKind.Explicit)]
    public class TestClassExplicit
    {
        [FieldOffset(4)]
        public uint A;
        [FieldOffset(4)]
        public int B;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct TestStructExplicit
    {
        [FieldOffset(4)]
        public uint A;
        [FieldOffset(4)]
        public int B;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class TestClassSequential
    {
        public int A;
        public int B;
        public byte C;
        public int D;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TestStructSequential
    {
        public int A;
        public int B;
        public byte C;
        public int D;
    }

    public unsafe static void Main()
    {
        // Explicit class
        var testClassExplicit = new TestClassExplicit();
        testClassExplicit.A = 32;

        System.Console.WriteLine(testClassExplicit.A);
        System.Console.WriteLine(testClassExplicit.B);

        testClassExplicit.B = 64;

        System.Console.WriteLine(testClassExplicit.A);
        System.Console.WriteLine(testClassExplicit.B);

        // Explicit struct
        var testStructExplicit = new TestStructExplicit();
        testStructExplicit.A = 32;

        System.Console.WriteLine(testStructExplicit.A);
        System.Console.WriteLine(testStructExplicit.B);

        testStructExplicit.B = 64;

        System.Console.WriteLine(testStructExplicit.A);
        System.Console.WriteLine(testStructExplicit.B);

        // Sequential class (with pack)
        var testClassSequential = new TestClassSequential { D = 18 };
        fixed (int* a = &testClassSequential.A)
        fixed (int* b = &testClassSequential.B)
        fixed (byte* c = &testClassSequential.C)
        fixed (int* d = &testClassSequential.D)
        {
            System.Console.WriteLine((byte*)b - (byte*)a);
            System.Console.WriteLine((byte*)c - (byte*)a);
            System.Console.WriteLine((byte*)d - (byte*)a);
            System.Console.WriteLine(testClassSequential.D);
        }

        // Sequential struct (with pack)
        var testStructSequential = new TestStructSequential() { D = 22 };
        System.Console.WriteLine((byte*)&testStructSequential.B - (byte*)&testStructSequential.A);
        System.Console.WriteLine((byte*)&testStructSequential.C - (byte*)&testStructSequential.A);
        System.Console.WriteLine((byte*)&testStructSequential.D - (byte*)&testStructSequential.A);
        System.Console.WriteLine(testStructSequential.D);
    }
}