﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PerformanceSummaryToCsv
{
    class Program
    {
        async static Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<FileInfo[]>(
                    "--inputs",
                    "Input text files containing an MSBuild PerformanceSummary section",
                    ArgumentArity.OneOrMore),
                new Option<FileInfo>(
                    "--output",
                    getDefaultValue: () => new FileInfo("MSBuild_performance.csv"),
                    "Path of the final csv file.")
            };

            rootCommand.Description = "Converts and aggregates MSBuild text performance summaries into a CSV.";

            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<FileInfo[], FileInfo>(Aggregate);

            // Parse the incoming args and invoke the handler
            return await rootCommand.InvokeAsync(args);
        }

        async static Task Aggregate(FileInfo[] inputs, FileInfo output)
        {
            Console.WriteLine($"Aggregating {string.Join(", ", inputs.Select(fi => fi.FullName))}");

            AggregateData aggregate = new();

            int numCols = inputs.Length;

            foreach (FileInfo? item in inputs)
            {
                await ReadFile(aggregate, item);
            }

            await aggregate.WriteCsv(output.FullName);
        }

        async static Task ReadFile(AggregateData aggregate, FileInfo item)
        {
            using var file = new StreamReader(item.FullName);

            List<TaskSummary> tasks = new();

            string? line = string.Empty;

            while (line != "Task Performance Summary:")
            {
                // Fast-forward through most of the file
                line = await file.ReadLineAsync();

                if (line is null)
                {
                    throw new FileFormatException($"File {item.FullName} didn't have a performance summary.");
                }
            }

            line = await file.ReadLineAsync(); // blank line

            if (line is null)
            {
                throw new FileFormatException($"File {item.FullName} ended prematurely.");
            }

            while (TaskSummary.TryParse(line, out var summary))
            {
                tasks.Add(summary);

                line = await file.ReadLineAsync();

                if (line is null)
                {
                    break;
                }
            }

            aggregate.AddBuild(item.Name, tasks);
        }
    }
}
