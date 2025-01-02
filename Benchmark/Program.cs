using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CoreRun;
using BenchmarkDotNet.Toolchains.CsProj;
using Kzrnm.Numerics;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using MyBigInteger = Kzrnm.Numerics.BigInteger;
using OrigBigInteger = System.Numerics.BigInteger;
#if NET8_0_OR_GREATER
using PortBigInteger = Kzrnm.Numerics.Port.BigInteger;
#endif

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
        AddJob(Job.ShortRun.WithToolchain(CsProjCoreToolchain.NetCoreApp80));
        //AddJob(Job.Default.WithToolchain(CsProjCoreToolchain.NetCoreApp70));

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

    [Benchmark(Baseline = true)] public OrigBigInteger Orig() => OrigBigInteger.Parse(s);

    [Benchmark] public MyBigInteger New() => MyBigInteger.Parse(s);
#if NET8_0_OR_GREATER
    [Benchmark] public PortBigInteger Port() => PortBigInteger.Parse(s);
#endif
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
#if NET8_0_OR_GREATER
    PortBigInteger port;
#endif


    [GlobalSetup]
    public void Setup()
    {
        var bytes = new byte[N];
        rnd.NextBytes(bytes);
        orig = new(bytes, isUnsigned: true);
        my = new(bytes, isUnsigned: true);
#if NET8_0_OR_GREATER
        port = new(bytes, isUnsigned: true);
#endif
    }

    [Benchmark(Baseline = true)]
    public string Orig() => orig.ToString();

    [Benchmark] public string New() => my.ToString();
#if NET8_0_OR_GREATER
    [Benchmark] public string Port() => port.ToString();
#endif
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
#if NET8_0_OR_GREATER
    PortBigInteger port1, port2;
#endif

    [GlobalSetup]
    public void Setup()
    {
        var bytes1 = new byte[N];
        var bytes2 = new byte[N / 2];
        rnd.NextBytes(bytes1);
        rnd.NextBytes(bytes2);
        orig1 = new(bytes1, isUnsigned: true);
        orig2 = new(bytes2, isUnsigned: true);
        my1 = new(bytes1, isUnsigned: true);
        my2 = new(bytes2, isUnsigned: true);
#if NET8_0_OR_GREATER
        port1 = new(bytes1, isUnsigned: true);
        port2 = new(bytes2, isUnsigned: true);
#endif
    }

    [Benchmark(Baseline = true)] public OrigBigInteger Orig() => orig1 + orig2;

    [Benchmark] public MyBigInteger New() => my1 + my2;
#if NET8_0_OR_GREATER
    [Benchmark] public PortBigInteger Port() => port1 + port2;
#endif
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
#if NET8_0_OR_GREATER
    PortBigInteger port1, port2;
#endif

    [GlobalSetup]
    public void Setup()
    {
        var bytes1 = new byte[N];
        var bytes2 = new byte[N / 2];
        rnd.NextBytes(bytes1);
        rnd.NextBytes(bytes2);
        orig1 = new(bytes1, isUnsigned: true);
        orig2 = new(bytes2, isUnsigned: true);
        my1 = new(bytes1, isUnsigned: true);
        my2 = new(bytes2, isUnsigned: true);
#if NET8_0_OR_GREATER
        port1 = new(bytes1, isUnsigned: true);
        port2 = new(bytes2, isUnsigned: true);
#endif
    }

    [Benchmark(Baseline = true)] public OrigBigInteger Orig() => orig1 * orig2;

    [Benchmark] public MyBigInteger New() => my1 * my2;
#if NET8_0_OR_GREATER
    [Benchmark] public PortBigInteger Port() => port1 * port2;
#endif
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
#if NET8_0_OR_GREATER
    PortBigInteger port1, port2;
#endif

    [GlobalSetup]
    public void Setup()
    {
        var bytes1 = new byte[N];
        var bytes2 = new byte[N / 2];
        rnd.NextBytes(bytes1);
        rnd.NextBytes(bytes2);
        orig1 = new(bytes1, isUnsigned: true);
        orig2 = new(bytes2, isUnsigned: true);
        my1 = new(bytes1, isUnsigned: true);
        my2 = new(bytes2, isUnsigned: true);
#if NET8_0_OR_GREATER
        port1 = new(bytes1, isUnsigned: true);
        port2 = new(bytes2, isUnsigned: true);
#endif
    }

    [Benchmark(Baseline = true)] public OrigBigInteger Orig() => orig1 / orig2;

    [Benchmark] public MyBigInteger New() => my1 / my2;
#if NET8_0_OR_GREATER
    [Benchmark] public PortBigInteger Port() => port1 / port2;
#endif
}