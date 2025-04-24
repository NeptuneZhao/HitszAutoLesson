using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hal_Windows.HitIdsAuto
{
	internal partial class HitszKebiao(string JsonLikeLesson, SchoolCalendar Calendar)
	{
		public void OnInitialize()
		{
			Read();

			ListSubjectInOrder();
		}

		private void Read()
		{
			List<JsonClass> jsonClasses = JsonSerializer.Deserialize<List<JsonClass>>(JsonLikeLesson);

			List<Subject> subjects = [];

			foreach (JsonClass class_ in jsonClasses)
			{
				Match match = SubjectRegex().Match(class_.SKSJ);
				if (match.Success && class_.KEY != "bz")
					subjects.Add(new Subject(match.Groups[1].Value, class_.KEY));
			}

			InSubjects = subjects;
		}

		private void CalculateDate()
		{

		}

		private void ListSubjectInOrder()
		{
			int maxLessonWeek = 0;
			foreach (Subject subject in InSubjects)
			{
				int max = subject.Weeks.Max();
				if (max > maxLessonWeek) maxLessonWeek = max;
			}
			SemesterWeek = maxLessonWeek;
		}

		[GeneratedRegex(@"""SKSJ"":""([^""]+)"",")]
		private static partial Regex SubjectRegex();

		private List<Subject> InSubjects;

		private int SemesterWeek;

		private class JsonClass
		{
			public string SKSJ { get; set; } = string.Empty;
			public string KEY { get; set; } = string.Empty;
		}

	}

    internal partial struct Subject
	{
		/// <summary>
		/// 
		/// </summary>
		/// <param name="lessonInfo">The Json Key: "SKSJ"</param>
		/// <param name="day">The Json Key: "KEY"</param>
		public Subject(string lessonInfo, string day)
		{
			// Normal classes, not online
			if (lessonInfo.Contains("\\n"))
			{
				string[] reversedInfo = [.. lessonInfo.Split("\\n", StringSplitOptions.None).Reverse()];
				// Type 1: [Time] [Classroom] [Week] [Teacher] [Name]
				Match TimeMatch = TimeRegex1().Match(reversedInfo[0]);
				if (TimeMatch.Success)
				{
					TimeStart = Convert.ToInt32(TimeMatch.Groups[1].Value);
					TimeEnd = Convert.ToInt32(TimeMatch.Groups[2].Value);

					string[] temp = reversedInfo[1].Split("][");
					Classroom = temp[1].Trim('[', ']');
					Weeks = GetWeek(temp[0].Trim('[', ']'));

					Teacher = reversedInfo[2].Trim('[', ']');
					
					for (int i = 2; i < reversedInfo.Length; i++)
						Name += reversedInfo[i].Trim('[', ']');
					return;
				}

				string[] temp2 = reversedInfo[1].Split("][");
				TimeMatch = TimeRegex2().Match(temp2[0]);
				if (TimeMatch.Success)
				{
					TimeStart = Convert.ToInt32(TimeMatch.Groups[1].Value);
					TimeEnd = Convert.ToInt32(TimeMatch.Groups[2].Value);

					Classroom = reversedInfo[0].Trim('[', ']');
					Weeks = GetWeek(temp2[1].Trim('[', ']'));
					Teacher = "没有老师";

					for (int i = 2; i < reversedInfo.Length; i++)
						Name += reversedInfo[i].Trim('[', ']');
				}
			}
		}

		public string Name { get; set; } = string.Empty;
		public string Teacher { get; set; }
		public string Classroom { get; set; }
		public List<int> Weeks { get; set; } = [];
		public int TimeStart { get; set; }
		public int TimeEnd { get; set; }
		public int Day { get; set; } = 0;

		[GeneratedRegex(@"第(\d+)-(\d+)节")]
		private static partial Regex TimeRegex1();

		[GeneratedRegex(@"(\d+)-(\d+)节")]
		private static partial Regex TimeRegex2();

		[GeneratedRegex(@"^\d+\s*-\s*\d+$")]
		private static partial Regex WeekRegex();

		private static List<int> GetWeek(string week)
		{
			List<int> weeks = [];

			string cleanedInput = week.Trim('[', ']').Replace("周", "").Trim();

			if (string.IsNullOrWhiteSpace(cleanedInput)) return weeks;

			string[] parts = cleanedInput.Split(',');

			foreach (string part in parts)
			{
				string trimmedPart = part.Trim();

				if (WeekRegex().IsMatch(trimmedPart))
				{
					string[] rangeParts = trimmedPart.Split('-');
					if (rangeParts.Length == 2 &&
						int.TryParse(rangeParts[0].Trim(), out int start) &&
						int.TryParse(rangeParts[1].Trim(), out int end)
					) if (start <= end) for (int i = start; i <= end; i++) weeks.Add(i);
				}
				else if (int.TryParse(trimmedPart, out int singleWeek))
					weeks.Add(singleWeek);
			}

			weeks = [.. new HashSet<int>(weeks)];
			weeks.Sort();

			return weeks;
		}

	}

	internal struct DayCalendar()
	{
		public string Today;
		public int Week, Day;
		public Subject[] TodaySubject = new Subject[12];
	}
}
