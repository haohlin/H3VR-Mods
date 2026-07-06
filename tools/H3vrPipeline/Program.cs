namespace H3vrPipeline;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 3 || !string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: H3vrPipeline validate <package.zip> <legacy-flat|bepinex>");
            return 2;
        }

        var result = PackageValidator.Validate(args[1], args[2]);
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine(error);
        }

        return result.IsValid ? 0 : 1;
    }
}
