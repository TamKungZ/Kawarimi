static class T
{
    public static int Fails;
    public static void Check(string name, bool ok) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}"); if (!ok) Fails++; }
    public static bool Throws<E>(Action a) where E : Exception { try { a(); return false; } catch (E) { return true; } }
}

static class Program
{
    static int Main()
    {
        RegistryTests.Run();
        ModLoaderTests.Run();
        XfbinTests.Run();
        SkinBuilderTests.Run();
        FormatsTests.Run();
        Console.WriteLine(T.Fails == 0 ? "ALL PASSED" : $"{T.Fails} FAILED");
        return T.Fails;
    }
}
