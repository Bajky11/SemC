using SemC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class Program
{

    private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
    private static string fileName = "data.txt";
    private static int bufferSize = 10000; //5, 1000000
    private static int dataSize = 10000000; //100, 10000000
    private static int NUM_TRIALS = 10;
    private static bool RUN_PERFORMANCE_TEST = true;
    private static bool debug = false;
    static Generator generator = new();
    static Stopwatch stopwatchG = new Stopwatch();
    static Stopwatch waitTimerG = new Stopwatch();
    static Stopwatch fileMetricsTimerG = new Stopwatch();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting tests...");

        await TestWriteToFile();
        await TestReadFromFile();

        if (RUN_PERFORMANCE_TEST)
        {
            await PerformanceTest(NUM_TRIALS);
        }

        Console.WriteLine("\nAll tests completed.");
    }

    static async Task TestReadFromFile()
    {
        Console.WriteLine("\nTesting single buffer read:");
        var singleTime = await ReadFromFile(useDoubleBuffering: false);
        Console.WriteLine($"Single buffer method completed in {singleTime} ms.");

        Console.WriteLine("\nTesting double buffer read:");
        var doubleTime = await ReadFromFile(useDoubleBuffering: true);
        Console.WriteLine($"Double buffer method completed in {doubleTime} ms.");
    }

    static async Task TestWriteToFile()
    {
        Console.WriteLine("\nTesting single buffer write:");
        var singleTime = await WriteToFile(useDoubleBuffering: false);
        Console.WriteLine($"Single buffer method completed in {singleTime} ms.");

        Console.WriteLine("\nTesting double buffer write:");
        var doubleTime = await WriteToFile(useDoubleBuffering: true);
        Console.WriteLine($"Double buffer method completed in {doubleTime} ms.");
    }

    static async Task PerformanceTest(int iterations)
    {
        Console.WriteLine($"\nPerformance test for {iterations} trials:");
        long totalSingleWriteTime = 0, totalDoubleWriteTime = 0;
        long totalSingleReadTime = 0, totalDoubleReadTime = 0;

        for (int i = 0; i < iterations; i++)
        {
            // Test writing to file
            File.Delete(fileName);
            var singleWriteTime = await WriteToFile(useDoubleBuffering: false);
            totalSingleWriteTime += singleWriteTime;

            File.Delete(fileName);
            var doubleWriteTime = await WriteToFile(useDoubleBuffering: true);
            totalDoubleWriteTime += doubleWriteTime;

            // Test reading from file
            var singleReadTime = await ReadFromFile(useDoubleBuffering: false);
            totalSingleReadTime += singleReadTime;

            var doubleReadTime = await ReadFromFile(useDoubleBuffering: true);
            totalDoubleReadTime += doubleReadTime;
        }

        // Printing write results
        Console.WriteLine($"Average write time for single buffer: {totalSingleWriteTime / iterations} ms.");
        Console.WriteLine($"Average write time for double buffer: {totalDoubleWriteTime / iterations} ms.");

        // Printing read results
        Console.WriteLine($"Average read time for single buffer: {totalSingleReadTime / iterations} ms.");
        Console.WriteLine($"Average read time for double buffer: {totalDoubleReadTime / iterations} ms.");
    }

    static void ResetMetrics()
    {
        stopwatchG.Reset();
        waitTimerG.Reset();
    }

    static async Task<long> WriteToFile(bool useDoubleBuffering)
    {
        generator.Reset();
        ResetMetrics();

        SemC.Buffer bufferOne = new SemC.Buffer(bufferSize, "Buffer 1");
        SemC.Buffer bufferTwo = new SemC.Buffer(bufferSize, "Buffer 2");
        Task bufferOneSaving = Task.CompletedTask;
        Task bufferTwoSaving = Task.CompletedTask;
        int activeBuffer = 1;

        stopwatchG.Start();
        while (generator.Count() < dataSize)
        {
            if (activeBuffer == 1)
            {
                await bufferOneSaving;
                bufferOne.Clear();

                while (!bufferOne.IsFull() && generator.Count() < dataSize)
                {
                    bufferOne.Add(generator.Next());
                }
                bufferOneSaving = SaveToFile(bufferOne.Items.ToList(), bufferOne.name);
                if (useDoubleBuffering) activeBuffer = 2;
            }
            else
            {
                await bufferTwoSaving;
                bufferTwo.Clear();

                while (!bufferTwo.IsFull() && generator.Count() < dataSize)
                {
                    bufferTwo.Add(generator.Next());
                }
                bufferTwoSaving = SaveToFile(bufferTwo.Items.ToList(), bufferTwo.name);
                if (useDoubleBuffering) activeBuffer = 1;
            }
        }

        await bufferOneSaving;
        await bufferTwoSaving;

        stopwatchG.Stop();
        return stopwatchG.ElapsedMilliseconds;
    }


    static async Task SaveToFile(List<int> buffer, string bufferName)
    {
        bool saveDebug = false;

        if (semaphore.CurrentCount == 0) if (saveDebug) Console.WriteLine("Waitig for file");

        await semaphore.WaitAsync();
        try
        {
            if (saveDebug) Console.WriteLine("Starting save");

            using (var fileStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                foreach (int item in buffer)
                {
                    await writer.WriteLineAsync(item.ToString());
                }
                await writer.FlushAsync();
            }
        }
        finally
        {
            if (saveDebug) Console.WriteLine($"{bufferName} save complete in: {fileMetricsTimerG.ElapsedMilliseconds} ms.");
            semaphore.Release();
        }
    }


    static async Task<long> ReadFromFile(bool useDoubleBuffering)
    {
        ResetMetrics();

        SemC.Buffer bufferOne = new SemC.Buffer(bufferSize, "Buffer 1");
        SemC.Buffer bufferTwo = new SemC.Buffer(bufferSize, "Buffer 2");
        int activeBuffer = 1;

        using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
        using (var reader = new StreamReader(fileStream))
        {
            stopwatchG.Start();
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                int number = int.Parse(line);
                if (activeBuffer == 1)
                {
                    bufferOne.Add(number);
                    if (bufferOne.IsFull())
                    {
                        await ProcessBuffer(bufferOne);
                        bufferOne.Clear();
                        if (useDoubleBuffering) activeBuffer = 2;
                    }
                }
                else
                {
                    bufferTwo.Add(number);
                    if (bufferTwo.IsFull())
                    {
                        await ProcessBuffer(bufferTwo);
                        bufferTwo.Clear();
                        if (useDoubleBuffering) activeBuffer = 1;
                    }
                }
            }

            // Zpracování zbývajícího obsahu v bufferech
            if (!bufferOne.IsEmpty()) await ProcessBuffer(bufferOne);
            if (!bufferTwo.IsEmpty()) await ProcessBuffer(bufferTwo);

            stopwatchG.Stop();
        }

        return stopwatchG.ElapsedMilliseconds;
    }

    static async Task ProcessBuffer(SemC.Buffer buffer)
    {
        if (debug) Console.WriteLine($"Processing {buffer.name} with {buffer.Count()} items.");
        //await Task.Delay(1); //Simulace zpracování bufferu
    }


}


