using AudibleApi.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileLiberator;

public partial class DownloadOptions
{
	// All provider coordinates are integer milliseconds. Validate before conversion
	// so neither checked arithmetic nor TimeSpan construction can partially mutate input.
	private const long MaximumChapterMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;

	private static void ValidateChapter(Chapter chapter)
	{
		if (chapter is null || chapter.StartOffsetMs < 0 || chapter.LengthMs < 0 ||
			chapter.StartOffsetMs > MaximumChapterMilliseconds ||
			chapter.LengthMs > MaximumChapterMilliseconds - chapter.StartOffsetMs)
			throw new InvalidDataException("Chapter coordinates are negative or exceed the supported timeline.");
	}

	private static long ChapterEnd(Chapter chapter) => chapter.StartOffsetMs + chapter.LengthMs;

	private static Chapter CopyChapter(Chapter chapter) => new()
	{
		Title = chapter.Title,
		StartOffsetMs = chapter.StartOffsetMs,
		StartOffsetSec = chapter.StartOffsetMs / 1000,
		LengthMs = chapter.LengthMs
	};

	private static void ValidateChapterOrder(IList<Chapter> chapters)
	{
		long previousEnd = 0;
		foreach (var chapter in chapters)
		{
			ValidateChapter(chapter);
			if (chapter.LengthMs == 0)
				continue;
			if (chapter.StartOffsetMs < previousEnd)
				throw new InvalidDataException("Chapter audio intervals overlap.");
			previousEnd = ChapterEnd(chapter);
		}
	}

	internal static List<Chapter> PrepareChapterTimeline(IList<Chapter>? provider, string? titleConcat)
	{
		var chapters = flattenChapters(provider, titleConcat).Where(c => c.LengthMs > 0).ToList();
		if (chapters.Count == 0)
			throw new InvalidDataException("Chapter metadata contains no playable intervals.");

		// ChapterInfo stores contiguous durations, not independent starts. Preserve
		// leading and interior audio gaps in the adjacent chapter instead of dropping
		// those milliseconds and silently moving every later chapter earlier.
		var firstEnd = ChapterEnd(chapters[0]);
		chapters[0].StartOffsetMs = 0;
		chapters[0].StartOffsetSec = 0;
		chapters[0].LengthMs = firstEnd;
		for (int index = 0; index + 1 < chapters.Count; index++)
			chapters[index].LengthMs = chapters[index + 1].StartOffsetMs - chapters[index].StartOffsetMs;
		return chapters;
	}
	internal void ReconcileChapterDuration(TimeSpan presentedDuration)
	{
		if (presentedDuration <= TimeSpan.Zero)
			throw new InvalidDataException("The input contains no playable audio duration.");

		var outro = Config.StripAudibleBrandAudio
			? TimeSpan.FromMilliseconds(ContentMetadata.ChapterInfo.BrandOutroDurationMs)
			: TimeSpan.Zero;
		if (outro < TimeSpan.Zero || outro >= presentedDuration)
			throw new InvalidDataException("Branding exceeds the available audio duration.");

		// Provider chapter coordinates have millisecond precision. A requested outro
		// trim cannot safely use an incomplete/contradictory provider endpoint.
		if (outro > TimeSpan.Zero &&
			Math.Abs((presentedDuration - outro - declaredChapterInfo.EndOffset).Ticks) > TimeSpan.TicksPerMillisecond)
			throw new InvalidDataException("The branding endpoint does not match the input audio duration.");

		var end = presentedDuration - outro;
		if (declaredChapterInfo.StartOffset < TimeSpan.Zero || declaredChapterInfo.StartOffset >= end)
			throw new InvalidDataException("The chapter start exceeds the available audio duration.");

		var replacement = new Mpeg4Lib.ChapterInfo(declaredChapterInfo.StartOffset);
		var expectedStart = declaredChapterInfo.StartOffset;
		foreach (var chapter in declaredChapterInfo)
		{
			if (chapter.Duration <= TimeSpan.Zero || chapter.StartOffset != expectedStart)
				throw new InvalidDataException("The prepared chapter timeline is not contiguous and positive.");
			expectedStart = chapter.EndOffset;
		}

		for (int index = 0; index < declaredChapterInfo.Count; index++)
		{
			var chapter = declaredChapterInfo.Chapters[index];
			if (chapter.StartOffset >= end)
				break;
			var chapterEnd = index == declaredChapterInfo.Count - 1 || chapter.EndOffset > end
				? end : chapter.EndOffset;
			replacement.AddChapter(chapter.Title, chapterEnd - chapter.StartOffset);
		}
		if (replacement.Count == 0 || replacement.EndOffset != end)
			throw new InvalidDataException("The chapter timeline cannot cover the available audio.");

		// Publish atomically only after validation. Repeated reconciliation always
		// starts with the original owned declaration, including removed tail entries.
		chapterInfo = replacement;
	}

}
