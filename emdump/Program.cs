using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

namespace eMDump
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Process[] processes;

				if (args.Length > 0)
				{
					int processId;
					Process process;

					if (int.TryParse(args[0], out processId) &&
						(process = Process.GetProcessById(processId)) != null)
					{
						processes = new[] { process };
					}
					else
					{
						MessageBox.Show(
							"Memory dump not performed, because the process was not found. Ensure the application is running");
						return;
					}
				}
				else
				{
					processes = Process.GetProcessesByName("MailClient");
					if (processes.Length == 0)
						processes = Process.GetProcessesByName("eM Client");
					if (processes.Length == 0)
					{
						MessageBox.Show(
							"Memory dump not performed, because the process was not found. Ensure the application is running");
						return;
					}
				}

				string fileName = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					"eM Client Dump " + DateTime.Now.ToString("MM-dd-yyyy HH-mm-ss") + ".zip");
				using (var outputZipFile = new FileStream(fileName, FileMode.CreateNew))
				using (var zipArchive = new ZipArchive(outputZipFile, ZipArchiveMode.Create))
				{
					foreach (var process in processes)
					{
						string tempFileName = Path.GetTempFileName();
						if (Dumper.MiniDumpToFile(process, tempFileName))
							zipArchive.CreateEntryFromFile(tempFileName, process.Id + ".dmp");
						File.Delete(tempFileName);
					}
				}

				MessageBox.Show(String.Format("Memory dump complete. File location: {0}", fileName));
			}
			catch (Exception e)
			{
				MessageBox.Show("Memory dump failed because of the following error:\n" + e.Message);
			}
		}
	}
}
