using System;
using System.Text.RegularExpressions;

public class Program
{
    public static void Main()
    {
        string arguments = "-batch -fullscreen -portable -bigpicture -bios \"{bios_path}\" -- \"{rom_path}\"";
        string result = Regex.Replace(arguments, @"\s*-+[-a-zA-Z0-9_]+\s+""?\{bios_path\}""?", "");
        Console.WriteLine("Original: " + arguments);
        Console.WriteLine("Result: " + result);
    }
}
