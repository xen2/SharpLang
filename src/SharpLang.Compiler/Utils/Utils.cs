using System;
using System.IO;

namespace SharpLang.Compiler.Utils
{
    public class Utils
    {
        public static string GetTestsDirectory()
        {
            var directory = Directory.GetParent(Directory.GetCurrentDirectory());
            var testsSubPath = Path.Combine("src", "SharpLang.Compiler.Tests", "tests");

            while (directory != null)
            {
                var path = Path.Combine(directory.FullName, testsSubPath);

                if (Directory.Exists(path))
                    return path;

                directory = directory.Parent;
            }

            throw new Exception("Tests directory was not found");
        }
    }
}
