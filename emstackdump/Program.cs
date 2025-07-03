using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace emstackdump
{
	class Program
	{
		static void Main(string[] args)
		{
			IList<string> tempNetTraceFilenames = Array.Empty<string>();
			IList<string> tempEtlxFilenames = Array.Empty<string>();

			try
			{
				var publishedProcessesPids = DiagnosticsClient.GetPublishedProcesses().ToHashSet();
				var processIds = Process.GetProcessesByName("MailClient").Select(process => process.Id).Where(id => publishedProcessesPids.Contains(id)).ToList();

				if (processIds.Count == 0)
				{
					processIds = Process.GetProcessesByName("eM Client").Select(process => process.Id).Where(id => publishedProcessesPids.Contains(id)).ToList();
				}

				if (processIds.Count == 0)
				{
					MessageBox.Show("Memory dump not performed, because the process was not found. Ensure the application is running");
					return;
				}

				var tempPath = Path.Combine(Path.GetTempPath(), "em" + Guid.NewGuid().ToString());
				tempNetTraceFilenames = processIds.Select(id => tempPath + "." + id.ToString() + ".etlx").ToList();
				var collectionTasks = new List<Task>();
				for (int i = 0; i < processIds.Count; i++)
				{
					collectionTasks.Add(CollectTrace(processIds[i], tempNetTraceFilenames[i]));
				}
				Task.WaitAll(collectionTasks.ToArray());

				string fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "eM Client Stack Dump " + DateTime.Now.ToString("MM-dd-yyyy HH-mm-ss") + ".txt");
				using var outputFile = new StreamWriter(fileName, false);

				// using the generated trace file, symbolocate and compute stacks.
				using var symbolReader = new SymbolReader(System.IO.TextWriter.Null) { SymbolPath = SymbolPath.MicrosoftSymbolServerPath };
				tempEtlxFilenames = new List<string>();
				for (int i = 0; i < processIds.Count; i++)
				{
					var tempEtlxFilename = TraceLog.CreateFromEventPipeDataFile(tempNetTraceFilenames[i]);
					tempEtlxFilenames.Add(tempEtlxFilename);

					outputFile.WriteLine("-- Process {0} --", processIds[i]);
					
					using (var eventLog = new TraceLog(tempEtlxFilename))
					{
						var stackSource = new MutableTraceEventStackSource(eventLog) { OnlyManagedCodeStacks = true };
						var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader);
						computer.GenerateThreadTimeStacks(stackSource);

						var samplesForThread = new Dictionary<int, List<StackSourceSample>>();

						stackSource.ForEach((sample) =>
						{
							var stackIndex = sample.StackIndex;
							while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false).StartsWith("Thread ("))
								stackIndex = stackSource.GetCallerIndex(stackIndex);

						// long form for: int.Parse(threadFrame["Thread (".Length..^1)])
						// Thread id is in the frame name as "Thread (<ID>)"
						string template = "Thread (";
							string threadFrame = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
							int threadId = int.Parse(threadFrame.Substring(template.Length, threadFrame.Length - (template.Length + 1)));

							if (samplesForThread.TryGetValue(threadId, out var samples))
							{
								samples.Add(sample);
							}
							else
							{
								samplesForThread[threadId] = new List<StackSourceSample>() { sample };
							}
						});

						// For every thread recorded in our trace, print the first stack
						foreach (var (threadId, samples) in samplesForThread)
						{
							outputFile.WriteLine($"Found {samples.Count} stacks for thread 0x{threadId:X}");
							PrintStack(outputFile, threadId, samples[0], stackSource);
						}
					}
				}

				MessageBox.Show(String.Format("Memory dump complete. File location: {0}", fileName));
			}
			catch (Exception e)
			{
				MessageBox.Show("Memory dump failed because of the following error:\n" + e.Message);
			}
			finally
			{
				foreach (var fileName in tempNetTraceFilenames)
					File.Delete(fileName);
				foreach (var fileName in tempEtlxFilenames)
					File.Delete(fileName);
			}
		}

		static List<EventPipeProvider> providers = new List<EventPipeProvider>()
		{
			new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational)
		};

		private static async Task CollectTrace(int processId, string tempNetTraceFilename)
		{
			var client = new DiagnosticsClient(processId);
			using var session = client.StartEventPipeSession(providers);
			// collect a *short* trace with stack samples
			// the hidden '--duration' flag can increase the time of this trace in case 10ms
			// is too short in a given environment, e.g., resource constrained systems
			// N.B. - This trace INCLUDES rundown.  For sufficiently large applications, it may take non-trivial time to collect
			//        the symbol data in rundown.
			using (FileStream fs = File.OpenWrite(tempNetTraceFilename))
			{
				Task copyTask = session.EventStream.CopyToAsync(fs);
				await Task.Delay(TimeSpan.FromSeconds(5));
				session.Stop();
				await copyTask;
			}
		}

		private static void PrintStack(TextWriter output, int threadId, StackSourceSample stackSourceSample, StackSource stackSource)
		{
			output.WriteLine($"Thread (0x{threadId:X}):");
			var stackIndex = stackSourceSample.StackIndex;
			while (!stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false).StartsWith("Thread ("))
			{
				output.WriteLine($"  {stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), verboseName: false)}"
					.Replace("UNMANAGED_CODE_TIME", "[Native Frames]"));
				stackIndex = stackSource.GetCallerIndex(stackIndex);
			}
			output.WriteLine();
		}
	}
}
