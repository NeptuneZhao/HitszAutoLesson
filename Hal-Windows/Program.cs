using Hal_Windows.HitIdsAuto;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Hal_Windows
{
	internal static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		[SupportedOSPlatform("windows")]
		static async Task Main()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
			{
				Console.WriteLine("This application is only supported on Windows.");
				return;
			}

			// IdsLogin login = new("2023111656", Console.WriteLine);
			// await login.Start("zhaochenrui233");

			using StreamReader reader = new(new FileStream("lessons.json", FileMode.Open));
			new HitszKebiao(reader.ReadToEnd()).OnInitialize();

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Form1());
		}
	}
}
