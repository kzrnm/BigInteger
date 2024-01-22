using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CoreRun;
using BenchmarkDotNet.Toolchains.CsProj;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MyBigInteger = Kzrnm.Numerics.BigInteger;
using OrigBigInteger = System.Numerics.BigInteger;

public class BenchmarkConfig : ManualConfig
{
    static void Main(string[] args)
    {
#if DEBUG
        BenchmarkSwitcher.FromAssembly(typeof(BenchmarkConfig).Assembly).Run(args, new DebugInProcessConfig());
#else
        _ = BenchmarkRunner.Run(typeof(BenchmarkConfig).Assembly);
#endif
    }
    public BenchmarkConfig()
    {
        //AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(BenchmarkDotNet.Exporters.MarkdownExporter.GitHub);
        AddJob(Job.Default.WithToolchain(CsProjCoreToolchain.NetCoreApp80));
        AddJob(Job.Default.WithToolchain(CsProjCoreToolchain.NetCoreApp70));

        SummaryStyle = SummaryStyle.Default
        .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Value)
        //.WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond)
        ;
    }
}


[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class BigIntegerParse
{
    Random rnd = new Random(227);

    [Params(10, 1000, 100000)]
    public int N;

    string s;

    [GlobalSetup]
    public void Setup()
    {
        var chars = new char[N];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)('0' + rnd.Next(10));
        }
        chars[0] = (char)('1' + rnd.Next(9));
        s = new(chars);
    }

    [Benchmark(Baseline = true)]
    public OrigBigInteger Orig() => OrigBigInteger.Parse(s);

    [Benchmark]
    public MyBigInteger New() => MyBigInteger.Parse(s);
}

[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class BigIntegerToString
{
    Random rnd = new Random(227);

    [Params(10, 1000, 100000)]
    public int N;

    MyBigInteger my;
    OrigBigInteger orig;

    [GlobalSetup]
    public void Setup()
    {
        var bytes = new byte[N];
        rnd.NextBytes(bytes);
        orig = new OrigBigInteger(bytes, isUnsigned: true);
        my = new MyBigInteger(bytes, isUnsigned: true);
    }

    [Benchmark(Baseline = true)]
    public string Orig() => orig.ToString();

    [Benchmark(Baseline = true)]
    public string New() => my.ToString();
}

[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class BigIntegerAdd
{
    Random rnd = new Random(227);

    [Params(10, 1000, 100000)]
    public int N;

    MyBigInteger my1, my2;
    OrigBigInteger orig1, orig2;

    [GlobalSetup]
    public void Setup()
    {
        var bytes1 = new byte[N];
        var bytes2 = new byte[N / 2];
        rnd.NextBytes(bytes1);
        rnd.NextBytes(bytes2);
        orig1 = new OrigBigInteger(bytes1, isUnsigned: true);
        my1 = new MyBigInteger(bytes1, isUnsigned: true);
        orig2 = new OrigBigInteger(bytes2, isUnsigned: true);
        my2 = new MyBigInteger(bytes2, isUnsigned: true);
    }

    [Benchmark(Baseline = true)]
    public OrigBigInteger Orig() => orig1 + orig2;

    [Benchmark]
    public MyBigInteger New() => my1 + my2;
}

[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class BigIntegerMultiply
{
    Random rnd = new Random(227);

    [Params(10, 1000, 100000)]
    public int N;

    MyBigInteger my1, my2;
    OrigBigInteger orig1, orig2;

    [GlobalSetup]
    public void Setup()
    {
        var bytes1 = new byte[N];
        var bytes2 = new byte[N / 2];
        rnd.NextBytes(bytes1);
        rnd.NextBytes(bytes2);
        orig1 = new OrigBigInteger(bytes1, isUnsigned: true);
        my1 = new MyBigInteger(bytes1, isUnsigned: true);
        orig2 = new OrigBigInteger(bytes2, isUnsigned: true);
        my2 = new MyBigInteger(bytes2, isUnsigned: true);
    }

    [Benchmark(Baseline = true)]
    public OrigBigInteger Orig() => orig1 * orig2;

    [Benchmark]
    public MyBigInteger New() => my1 * my2;
}

[Config(typeof(BenchmarkConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class BigIntegerDivide
{
    Random rnd = new Random(227);

    [Params(10, 1000, 100000)]
    public int N;

    MyBigInteger my1, my2;
    OrigBigInteger orig1, orig2;

    [GlobalSetup]
    public void Setup()
    {
        var bytes1 = new byte[N];
        var bytes2 = new byte[N / 2];
        rnd.NextBytes(bytes1);
        rnd.NextBytes(bytes2);
        orig1 = new OrigBigInteger(bytes1, isUnsigned: true);
        my1 = new MyBigInteger(bytes1, isUnsigned: true);
        orig2 = new OrigBigInteger(bytes2, isUnsigned: true);
        my2 = new MyBigInteger(bytes2, isUnsigned: true);
    }

    [Benchmark(Baseline = true)]
    public OrigBigInteger Orig() => orig1 / orig2;

    [Benchmark]
    public MyBigInteger New() => my1 / my2;
}