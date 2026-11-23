using System.Diagnostics;
using System.Reflection;

namespace SmartNotes.Tests;

/// <summary>Marks a test method: public static void, no parameters. <see cref="E2E"/> tests only run with --e2e.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute
{
    public bool E2E { get; init; }
}

public sealed class TestFailure(string message) : Exception(message);

public static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new TestFailure(message);
    }

    public static void False(bool condition, string message) => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new TestFailure($"{what}: expected <{expected}>, got <{actual}>");
    }

    public static void NotNull(object? value, string what)
    {
        if (value == null) throw new TestFailure($"{what}: expected a value, got null");
    }

    public static void Contains(string expectedPart, string? actual, string what)
    {
        if (actual == null || !actual.Contains(expectedPart, StringComparison.Ordinal))
            throw new TestFailure($"{what}: expected to contain <{expectedPart}>, got <{actual}>");
    }

    /// <summary>Polls until the condition holds (UI and debounced saves are asynchronous).</summary>
    public static void Eventually(Func<bool> condition, string message, int timeoutMs = 6000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try { if (condition()) return; } catch { /* not ready yet */ }
            Thread.Sleep(100);
        }
        if (!condition()) throw new TestFailure($"{message} (waited {timeoutMs} ms)");
    }
}

/// <summary>The data folder every SmartNotes service uses in this process (set before any SmartNotes type loads).</summary>
public static class TestData
{
    public static readonly string Root = Path.Combine(Path.GetTempPath(), "SmartNotesTests");
    public static readonly string UnitDataDir = Path.Combine(Root, "unit-data");

    /// <summary>Empties the unit-test data folder (the folder itself stays: services cache its path).</summary>
    public static void ResetUnitDataDir()
    {
        Directory.CreateDirectory(UnitDataDir);
        foreach (var file in Directory.GetFiles(UnitDataDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        foreach (var dir in Directory.GetDirectories(UnitDataDir)) Directory.Delete(dir, recursive: true);
        // a "trash" left as a plain file by a test
        string trash = Path.Combine(UnitDataDir, "trash");
        if (File.Exists(trash)) File.Delete(trash);
    }

    public static string PathIn(string name) => Path.Combine(UnitDataDir, name);
}

public static class Runner
{
    public static int Run(string[] args)
    {
        bool e2e = args.Contains("--e2e");
        string? filter = args.FirstOrDefault(a => !a.StartsWith("--"));

        var tests = typeof(Runner).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Method: m, Attr: m.GetCustomAttribute<TestAttribute>()))
            .Where(x => x.Attr != null && (e2e || !x.Attr.E2E))
            .Where(x => filter == null || $"{x.Method.DeclaringType!.Name}.{x.Method.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Attr!.E2E).ThenBy(x => x.Method.DeclaringType!.Name).ThenBy(x => x.Method.Name)
            .ToList();

        int failed = 0;
        var sw = Stopwatch.StartNew();
        foreach (var (method, attr) in tests)
        {
            string name = $"{method.DeclaringType!.Name}.{method.Name}";
            var one = Stopwatch.StartNew();
            try
            {
                if (!attr!.E2E) TestData.ResetUnitDataDir();
                method.Invoke(null, null);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"PASS  {name} ({one.ElapsedMilliseconds} ms)");
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                var ex = tie.InnerException;
                Console.WriteLine($"FAIL  {name}: {(ex is TestFailure ? ex.Message : ex.ToString())}");
            }
            finally
            {
                Console.ResetColor();
            }
        }
        Console.WriteLine($"\n{tests.Count - failed}/{tests.Count} passed in {sw.Elapsed.TotalSeconds:F1} s" + (e2e ? "" : "  (end-to-end tests skipped; add --e2e)"));
        return failed;
    }
}
