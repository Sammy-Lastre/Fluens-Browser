using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using Fluens.UI.Services.AdBlocking;
using Microsoft.VSDiagnostics;

namespace Fluens.UI.Benchmarks;
[CPUUsageDiagnoser]
public class AdBlockRuleParserBenchmark
{
    private string _millionHostRules = string.Empty;
    [GlobalSetup]
    public void GlobalSetup()
    {
        StringBuilder builder = new(capacity: 28_000_000);
        for (int i = 0; i < 1_000_000; i++)
        {
            builder.Append("||ads");
            builder.Append(i);
            builder.Append(".example.com^\n");
        }

        _millionHostRules = builder.ToString();
    }

    [Benchmark]
    public int ParseMillionHostRules()
    {
        return AdBlockRuleParser.Parse(_millionHostRules).Count;
    }
}