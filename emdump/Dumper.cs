using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;

namespace eMDump
{
	public class Dumper
	{
		internal enum MINIDUMP_TYPE
		{
			MiniDumpNormal = 0x00000000,
			MiniDumpWithDataSegs = 0x00000001,
			MiniDumpWithFullMemory = 0x00000002,
			MiniDumpWithHandleData = 0x00000004,
			MiniDumpFilterMemory = 0x00000008,
			MiniDumpScanMemory = 0x00000010,
			MiniDumpWithUnloadedModules = 0x00000020,
			MiniDumpWithIndirectlyReferencedMemory = 0x00000040,
			MiniDumpFilterModulePaths = 0x00000080,
			MiniDumpWithProcessThreadData = 0x00000100,
			MiniDumpWithPrivateReadWriteMemory = 0x00000200,
			MiniDumpWithoutOptionalData = 0x00000400,
			MiniDumpWithFullMemoryInfo = 0x00000800,
			MiniDumpWithThreadInfo = 0x00001000,
			MiniDumpWithCodeSegs = 0x00002000,
			MiniDumpWithoutAuxiliaryState = 0x4000,
			MiniDumpWithFullAuxiliaryState = 0x8000,
			MiniDumpWithPrivateWriteCopyMemory = 0x10000,
			MiniDumpIgnoreInaccessibleMemory = 0x20000,
			MiniDumpWithTokenInformation = 0x40000
		}

		[DllImport("dbghelp.dll")]
		static extern bool MiniDumpWriteDump(
			IntPtr hProcess,
			Int32 ProcessId,
			IntPtr hFile,
			MINIDUMP_TYPE DumpType,
			IntPtr ExceptionParam,
			IntPtr UserStreamParam,
			IntPtr CallackParam);

		public static bool MiniDumpToFile(Process process, string filename)
		{
			using (FileStream fsToDump = new FileStream(filename, FileMode.Create))
			{
				MiniDumpWriteDump(process.Handle, process.Id,
					fsToDump.SafeFileHandle.DangerousGetHandle(),
					MINIDUMP_TYPE.MiniDumpWithPrivateReadWriteMemory |
					MINIDUMP_TYPE.MiniDumpWithDataSegs |
					MINIDUMP_TYPE.MiniDumpWithHandleData |
					MINIDUMP_TYPE.MiniDumpWithUnloadedModules |
					MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo |
					MINIDUMP_TYPE.MiniDumpWithThreadInfo |
					MINIDUMP_TYPE.MiniDumpWithTokenInformation,
					IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				fsToDump.Close();
			}

			return true;
		}
	}
}
