using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Benchmarks.Cases.Results;

// In process: BenchmarkDotNet 0.15.8 does not recognise net11.0 out of process (owner's decision on #136).
var config = DefaultConfig.Instance.AddJob(Job.InProcess.AsMutator());

BenchmarkSwitcher.FromAssembly(typeof(ResultBenchmarks).Assembly).Run(args, config);
