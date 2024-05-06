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
    private static int bufferSize = 3;
    private static int dataSize = 10;
    private static int numTrials = 1;
    private static bool performanceTest = false;
    private static bool debug = false;

    static async Task Main(string[] args)
    {
        var (doubleAverageTime, doubleAverageWaitTime) = await PerformanceTest(DoubleBufferSave, removeOldFile: true);
        Console.WriteLine($"Double buffer method average completed in {doubleAverageTime} ms with average wait time of {doubleAverageWaitTime} ms.");

        var (singleAverageTime, singleAverageWaitTime) = await PerformanceTest(SingleBufferSave, removeOldFile: true);
        Console.WriteLine($"Single buffer method average completed in {singleAverageTime} ms with average wait time of {singleAverageWaitTime} ms.");

        if (performanceTest)
        {
            Console.WriteLine($"Starting performance comparison over {numTrials} trials.");

            //var (singleAverageTime, singleAverageWaitTime) = await PerformanceTest(SingleBufferSave, removeOldFile: true);
            Console.WriteLine($"Single buffer method average completed in {singleAverageTime} ms with average wait time of {singleAverageWaitTime} ms.");

            //var (doubleAverageTime, doubleAverageWaitTime) = await PerformanceTest(DoubleBufferSave, removeOldFile: true);
            Console.WriteLine($"Double buffer method average completed in {doubleAverageTime} ms with average wait time of {doubleAverageWaitTime} ms.");

            var (doubleLoadAverageTime, doubleLoadAverageWaitTime) = await PerformanceTest(DoubleBufferLoad);
            Console.WriteLine($"Double buffer load method average completed in {doubleLoadAverageTime} ms with average wait time of {doubleLoadAverageWaitTime} ms.");

            Console.WriteLine("Performance comparison completed.");
        }
    }

    static async Task<(double, double)> PerformanceTest(Func<Task<(long, long)>> saveMethod, bool removeOldFile = false)
    {
        long totalExecutionTime = 0;
        long totalWaitTime = 0;

        for (int i = 0; i < numTrials; i++)
        {
            if (removeOldFile) File.Delete(fileName); // Reset file for each trial
            var (executionTime, waitTime) = await saveMethod();
            totalExecutionTime += executionTime;
            totalWaitTime += waitTime;
        }

        return ((double)totalExecutionTime / numTrials, (double)totalWaitTime / numTrials);
    }

    static async Task<(long, long)> SingleBufferSave()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Stopwatch waitTimer = new Stopwatch();
        long totalWaitTime = 0;

        SemC.Buffer buffer = new SemC.Buffer(bufferSize, "Buffer 1");
        for (int i = 0; i < dataSize; i++)
        {
            buffer.Add(i);
            if (buffer.IsFull() || i == dataSize - 1)
            {
                waitTimer.Restart();
                await SaveToFile(buffer.Items.ToList(), buffer.name);
                waitTimer.Stop();
                totalWaitTime += waitTimer.ElapsedMilliseconds;

                buffer.Clear();
            }
        }

        stopwatch.Stop();
        return (stopwatch.ElapsedMilliseconds, totalWaitTime);
    }


    static async Task<(long, long)> DoubleBufferSave()
    {
        Stopwatch stopwatch = new Stopwatch();
        Stopwatch waitTimer = new Stopwatch();
        long totalWaitTime = 0;

        SemC.Buffer bufferOne = new SemC.Buffer(bufferSize, "Buffer 1");
        SemC.Buffer bufferTwo = new SemC.Buffer(bufferSize, "Buffer 2");
        Task bufferOneSaving = Task.CompletedTask;
        Task bufferTwoSaving = Task.CompletedTask;
        int activeBuffer = 1;
        int counter = 0;

        stopwatch.Restart();
        while (counter < dataSize)
        {
            if (activeBuffer == 1)
            {
                waitTimer.Reset();
                await bufferOneSaving;
                waitTimer.Stop();
                totalWaitTime += waitTimer.ElapsedMilliseconds;
                

                bufferOne.Clear();
                while (!bufferOne.IsFull() && counter < dataSize)
                {
                    bufferOne.Add(counter++);
                }
                bufferOneSaving = SaveToFile(bufferOne.Items.ToList(), bufferOne.name);
                activeBuffer = 2;
            }
            else
            {
                waitTimer.Start();
                await bufferTwoSaving;
                waitTimer.Stop();
                totalWaitTime += waitTimer.ElapsedMilliseconds;
                waitTimer.Reset();

                bufferTwo.Clear();
                while (!bufferTwo.IsFull() && counter < dataSize)
                {
                    bufferTwo.Add(counter++);
                }
                bufferTwoSaving = SaveToFile(bufferTwo.Items.ToList(), bufferTwo.name);
                activeBuffer = 1;
            }
        }

        await bufferOneSaving;
        await bufferTwoSaving;

        stopwatch.Stop();
        return (stopwatch.ElapsedMilliseconds, totalWaitTime);
    }

    static bool saveDebug = true;
    static Stopwatch saveStopWatch = new Stopwatch();
    static async Task SaveToFile(List<int> buffer, string bufferName)
    {
        if (semaphore.CurrentCount == 0)
        {
            if (saveDebug) Console.WriteLine("Waitig for file");
        }
        await semaphore.WaitAsync();
        if (saveDebug) Console.WriteLine("Starting save");
        try
        {
            saveStopWatch.Restart();
            using (var fileStream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                foreach (int item in buffer)
                {
                    await writer.WriteLineAsync(item.ToString());
                }
            }
            await Task.Delay(100); // Delay to simulate long save time
            saveStopWatch.Stop();
        }
        finally
        {
            if (saveDebug) Console.WriteLine($"{bufferName} save complete in: {saveStopWatch.ElapsedMilliseconds} ms.");
            semaphore.Release();
        }
    }

    static async Task<(long, long)> DoubleBufferLoad()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Stopwatch waitTimer = new Stopwatch();
        long totalWaitTime = 0;

        List<int> bufferOne = new List<int>();
        List<int> bufferTwo = new List<int>();
        Task<List<int>> bufferOneLoading = Task.FromResult(new List<int>());
        Task<List<int>> bufferTwoLoading = Task.FromResult(new List<int>());
        int activeBuffer = 1;

        bufferOneLoading = LoadFromFile(bufferSize);

        while (!bufferOneLoading.IsCompleted || !bufferTwoLoading.IsCompleted)
        {
            if (activeBuffer == 1)
            {
                waitTimer.Start();
                bufferOne = await bufferOneLoading;
                waitTimer.Stop();
                totalWaitTime += waitTimer.ElapsedMilliseconds;
                waitTimer.Reset();

                ProcessData(bufferOne);  // Místo, kde se data zpracovávají

                if (debug) Console.WriteLine("Buffer 1 is processed, loading new data.");
                bufferOneLoading = LoadFromFile(bufferSize);
                activeBuffer = 2;
            }
            else
            {
                waitTimer.Start();
                bufferTwo = await bufferTwoLoading;
                waitTimer.Stop();
                totalWaitTime += waitTimer.ElapsedMilliseconds;
                waitTimer.Reset();

                ProcessData(bufferTwo);  // Místo, kde se data zpracovávají

                if (debug) Console.WriteLine("Buffer 2 is processed, loading new data.");
                bufferTwoLoading = LoadFromFile(bufferSize);
                activeBuffer = 1;
            }
        }

        stopwatch.Stop();
        return (stopwatch.ElapsedMilliseconds, totalWaitTime);
    }

    static async Task<List<int>> LoadFromFile(int count)
    {
        List<int> buffer = new List<int>();
        await semaphore.WaitAsync();
        try
        {
            using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var reader = new StreamReader(fileStream))
            {
                for (int i = 0; i < count; i++)
                {
                    if (reader.EndOfStream)
                        break;
                    string line = await reader.ReadLineAsync();
                    buffer.Add(int.Parse(line));
                    if (debug) Console.WriteLine($"Loaded: {line}");
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
        return buffer;
    }

    static void ProcessData(List<int> data)
    {
        Console.WriteLine("Printing buffer:");
        foreach (int oneData in data)
        {
            Console.WriteLine(oneData);
        }
    }
}


