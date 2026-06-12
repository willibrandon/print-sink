return await PrintSink.Cli.CliApplication
    .RunAsync(args, Console.Out, Console.Error, CancellationToken.None)
    .ConfigureAwait(false);
