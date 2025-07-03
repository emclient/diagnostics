using System.Runtime.InteropServices;

namespace System.Windows.Forms
{
	class MessageBox
	{
		[DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
		static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

		public static void Show(string text)
		{
			if (OperatingSystem.IsWindows())
			{
				MessageBoxW(IntPtr.Zero, text, null, 0);
			}
			else
			{
				Console.WriteLine(text);
			}
		}
	}
}
