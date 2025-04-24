
namespace Hal_Windows.HitIdsAuto
{
	using System;
    using System.Collections.Generic;
    using System.Net;
	using System.Net.Http;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Request = System.Collections.Generic.KeyValuePair<string, string>;
	public partial class IdsLogin : IDisposable
	{
		private async Task Login(string password, bool isDebug)
		{
			string ExecutionPage = await GetExecution();
			Log?.Invoke("Execution get.");

			while (!await ProcessCaptcha()) ;

			HttpResponseMessage WaitToCasResponse;

			while (true)
			{
				WaitToCasResponse = await OneClient.PostAsync(LoginUrl, GeneratePostData(password, ExecutionPage));
				if (WaitToCasResponse.StatusCode != HttpStatusCode.Found) // 302
				{
					LogStatus("CAS: failed");
					await Task.Delay(Timeout);
					continue;
				}
				else
					break;
			}

			LogStatus("CAS: OK");

			HttpResponseMessage WaitToTicketResponse;

			while (true)
			{
				WaitToTicketResponse = await OneClient.GetAsync(WaitToCasResponse.Headers.Location!.ToString());
				if (WaitToTicketResponse.StatusCode != HttpStatusCode.Found)
				{
					LogStatus("Ticket: failed");
					await Task.Delay(Timeout);
					continue;
				}
				else
					break;
			}

			LogStatus("Ticket: OK");

			SchoolCalendar Calendar = await GetCalendar();

			HttpResponseMessage aacResponse = await OneClient.GetAsync("http://jw.hitsz.edu.cn/authentication/main");

			LogStatus("Jiaowu: OK");

			// Keep online
			if (aacResponse.StatusCode == HttpStatusCode.OK)
				new Thread(new ThreadStart(async () => await KeepOnline())).Start();

			FormUrlEncodedContent lessonContent = new([
				new Request("xn", Calendar.Year),
				new Request("xq", Calendar.Semester)
			]);

			HttpResponseMessage lessonResponse = await OneClient.PostAsync("http://jw.hitsz.edu.cn/xszykb/queryxszykbzong", lessonContent);

			LogStatus("Lessons: OK");

			// Now
			if (lessonResponse.StatusCode == HttpStatusCode.OK)
			{
				string lessonHtml = await lessonResponse.Content.ReadAsStringAsync();
				new HitszKebiao(lessonHtml, Calendar).OnInitialize();
			}
		}

		// We need process it anyway in the future
		private async Task<bool> ProcessCaptcha()
		{
			if ((await OneClient.GetStringAsync($"https://ids.hit.edu.cn/authserver/checkNeedCaptcha.htl?username={Username}&_={GetTimeStamp()}")).Contains("true"))
			{
				LogStatus("Captcha: failed");

				_ = new ProcessCaptcha();

				await Task.Delay(Timeout);
				return await ProcessCaptcha();
			}
			else
			{
				LogStatus("Captcha: OK");
				return true;
			}
		}

		public IdsLogin(string username, LoggerOutside lo = null)
		{
			OneClient = new(new HttpClientHandler()
			{
				UseCookies = true,
				CookieContainer = new(),
				AllowAutoRedirect = false,
				ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
			});

			Username = username;
			Log = lo;
		}

		private const string LoginUrl = "https://ids.hit.edu.cn/authserver/login?service=http%3A%2F%2Fjw.hitsz.edu.cn%2FcasLogin";

		private const int Timeout = 10 * 1000;
		private readonly HttpClient OneClient;
		private readonly string Username;

		public delegate void LoggerOutside(string msg);
		private readonly LoggerOutside Log;


		public async Task Start(string password, bool isDebug = true) => await Login(password, isDebug);
		private void LogStatus(string msg) => Log?.Invoke(msg);
		private static string GetTimeStamp() => ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds().ToString();
		private static string ExtractExecution(string html)
		{
			Match match = ExecutionRegex().Match(html);
			return match.Success ? match.Groups[1].Value : string.Empty;
		}

		private async Task<SchoolCalendar> GetCalendar()
		{
			SchoolCalendar cal = new();
			HttpResponseMessage calendarResponse = await OneClient.GetAsync("https://www.hit.edu.cn/ksfwtd/list.htm");
			if (calendarResponse.StatusCode == HttpStatusCode.OK)
			{
				string calendarHtml = await calendarResponse.Content.ReadAsStringAsync();
				Match match = CalendarLinkRegex().Match(calendarHtml);
				if (match.Success)
				{
					HttpResponseMessage calendarLinkResponse = await OneClient.GetAsync(match.Groups[1].Value);
					if (calendarLinkResponse.StatusCode == HttpStatusCode.OK)
					{
						string calendarContent = await calendarLinkResponse.Content.ReadAsStringAsync();

						Match match2 = YearAndSemesterRegex().Match(calendarContent);
						if (match2.Success)
						{
							cal.Year = $"{match2.Groups[1].Value}-{match2.Groups[2].Value}";
							cal.Semester = match2.Groups[3].Value;
						}

						Match match3 = StartSemesterRegex().Match(calendarContent);
						if (match3.Success)
						{
							cal.StartSemesterMonth = match3.Groups[1].Value;
							cal.StartSemesterDay = match3.Groups[2].Value;
							return cal;
						}
					}
				}
			}
			LogStatus("Calendar: failed");
			await Task.Delay(Timeout);
			return await GetCalendar();
		}

		private async Task<string> GetExecution()
		{
			try
			{
				return await OneClient.GetStringAsync(LoginUrl);
			}
			catch (HttpRequestException e)
			{
				LogStatus($"Error: {e.Message}");
				await Task.Delay(Timeout);
				return await GetExecution();
			}
		}

		private async Task KeepOnline()
		{
			StringContent onlineContent = new("{\"code\":0,\"msg\":null,\"msg_en\":null,\"content\":null}", Encoding.UTF8, "application/json");
			HttpResponseMessage response = await OneClient.PostAsync("http://jw.hitsz.edu.cn/component/online", onlineContent);

			if (response.StatusCode != HttpStatusCode.OK)
				throw new HttpIOException(HttpRequestError.ConnectionError, "Keep online failed, please check your Internet connection.");

			LogStatus($"Online at {DateTime.Now:F}\n");
			await Task.Delay(60 * 1000);
			await KeepOnline();
		}

		private FormUrlEncodedContent GeneratePostData(string password, string execution) => new([
			new Request("username", Username),
			new Request("password", EncryptAES.EncryptString(password, SaltRegex().Match(execution).Groups[1].Value)),
			new Request("dllt", "generalLogin"),
			new Request("cllt", "userNameLogin"),
			new Request("_eventId", "submit"),
			new Request("captcha", ""),
			new Request("execution", ExtractExecution(execution))
		]);

		void IDisposable.Dispose() => GC.SuppressFinalize(this);

		[GeneratedRegex(@"name=""execution"" value=""(.*?)""")]
		private static partial Regex ExecutionRegex();

		[GeneratedRegex(@"<input type=""hidden"" id=""pwdEncryptSalt"" value=""(.*?)""")]
		private static partial Regex SaltRegex();

		[GeneratedRegex(@"<a href=""([^""]+)"" target=""_blank"" sudyfile-attr=""{'title':'校历'}"" textvalue=""校历"">校历</a>")]
		private static partial Regex CalendarLinkRegex();

		[GeneratedRegex(@"(\d+)-(\d+)学年(\S)季学期校历")]
		private static partial Regex YearAndSemesterRegex();

		[GeneratedRegex(@"(\S)月.+&nbsp;(\d{2})<br />学期开始")]
		private static partial Regex StartSemesterRegex();

	}

	internal class ProcessCaptcha()
	{

	}

	internal class SchoolCalendar
	{
		public string Year;
		public string Semester { get { return semester; } set { semester = ToSemesterIndex(value); } }
		private string semester;

		public string StartSemesterMonth, StartSemesterDay;

		private static string ToSemesterIndex(string semester) => semester switch
		{
			"春" => "2",
			"夏" => "3",
			"秋" => "1",
			_ => throw new ArgumentException($"Unknown semester: {semester}")
		};

		public string GetSemesterChar() => Semester switch
		{
			"1" => "秋",
			"2" => "春",
			"3" => "夏",
			_ => throw new ArgumentException($"Unknown semester: {semester}")
		};
	}

	internal static class EncryptAES
	{
		public static string EncryptString(string psw, string salt)
		{
			if (salt.Length != 16)
				throw new ArgumentException("Salt length must be 16.");

			byte[] plainText = Encoding.UTF8.GetBytes(RandomString(64) + psw);

			using Aes aesAlg = Aes.Create();

			aesAlg.Key = Encoding.UTF8.GetBytes(salt);
			aesAlg.IV = Encoding.UTF8.GetBytes(RandomString(16));

			aesAlg.Mode = CipherMode.CBC;
			aesAlg.Padding = PaddingMode.PKCS7;

			return Convert.ToBase64String(aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV).TransformFinalBlock(plainText, 0, plainText.Length));
		}

		private const string AesChars = "ABCDEFGHJKMNPQRSTWXYZabcdefhijkmnprstwxyz2345678";
		private static string RandomString(int n)
		{
			string Eax = string.Empty;
			for (int i = 0; i < n; i++)
				Eax += AesChars[Convert.ToInt32(Math.Floor(new Random().NextDouble() * AesChars.Length))];

			return Eax;
		}
	}


}
