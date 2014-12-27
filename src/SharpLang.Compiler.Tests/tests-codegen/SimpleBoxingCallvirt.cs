public static class Program
{
	public interface ITest
	{
		void Print();
	}

    public struct Test : ITest
    {
        public int B;
		
		public void Print()
		{
			System.Console.WriteLine(B);
		}
	}
	
    public static void Main()
    {
		var test = new Test();
		test.Print();
		((ITest)test).Print();
    }
}