using AaxDecrypter;
using AudibleApi.Common;
using DataLayer;
using LibationFileManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileLiberator.Tests;

[TestClass]
[DoNotParallelize]
public class NetworkFileStreamContractTests
{
	private string directory = string.Empty;
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

	[TestInitialize]
	public void Initialize()
	{
		directory = Path.Combine(Path.GetTempPath(), "libation-http-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(directory);
	}

	[TestCleanup]
	public void Cleanup() => Directory.Delete(directory, recursive: true);

	[TestMethod]
	[DataRow("bytes 1-3/4", 3)]
	[DataRow("bytes 0-2/4", 4)]
	[DataRow("bytes 0-3/*", 4)]
	[DataRow("items 0-3/4", 4)]
	[DataRow("bytes 0-4/4", 5)]
	public async Task Invalid_range_headers_are_rejected_before_any_file_mutation(string range, int bodyLength)
	{
		await using var server = new LoopbackServer(Response.Partial(range, new string('x', bodyLength)));
		string path = Path.Combine(directory, "partial");
		File.WriteAllText(path, "preserved");
		using var stream = new NetworkFileStream(path, server.Uri);

		await AssertRejectedAsync(stream);

		Assert.AreEqual(0, stream.WritePosition);
		Assert.AreEqual("preserved", File.ReadAllText(path));
	}

	[TestMethod]
	public async Task Bounded_ranges_request_the_next_exact_offset_with_the_same_strong_validator()
	{
		await using var server = new LoopbackServer(
			Response.Partial("bytes 0-2/6", "abc", "\"one\""),
			Response.Partial("bytes 3-5/6", "def", "\"one\""));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);

		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);

		Assert.AreEqual(6, stream.WritePosition);
		Assert.AreEqual("abcdef", File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(2, server.Requests.Count);
		Assert.AreEqual("bytes=3-", server.Requests.Last()["Range"]);
		Assert.AreEqual("\"one\"", server.Requests.Last()["If-Range"]);
	}

	[TestMethod]
	[DataRow("\"two\"")]
	[DataRow(null)]
	[DataRow("W/\"one\"")]
	public async Task Resume_rejects_a_changed_missing_or_weak_response_validator(string? responseTag)
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 2-3/4", "CD", responseTag));
		using var stream = Resume(server.Uri, "ab", 4, "\"one\"");

		await AssertRejectedAsync(stream);

		Assert.AreEqual(2, stream.WritePosition);
		Assert.AreEqual("ab", File.ReadAllText(stream.SaveFilePath));
	}

	[TestMethod]
	[DataRow(null)]
	[DataRow("W/\"one\"")]
	public async Task An_unvalidated_saved_partial_is_not_silently_concatenated(string? savedTag)
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 2-3/4", "CD", "\"one\""));
		using var stream = Resume(server.Uri, "ab", 4, savedTag);

		await AssertRejectedAsync(stream);

		Assert.AreEqual("ab", File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(0, server.Requests.Count);
	}

	[TestMethod]
	public async Task A_valid_saved_partial_sends_IfRange_and_finishes_exactly_once()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 2-3/4", "cd", "\"one\""));
		using var stream = Resume(server.Uri, "ab", 4, "\"one\"");

		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);

		Assert.AreEqual("abcd", File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(1, server.Requests.Count);
		Assert.AreEqual("\"one\"", server.Requests.Single()["If-Range"]);
		Assert.AreEqual("\"one\"", JObject.Parse(JsonConvert.SerializeObject(stream))["EntityTag"]?.Value<string>());
	}

	[TestMethod]
	public async Task A_full_200_response_is_supported_only_for_a_fresh_download()
	{
		await using var server = new LoopbackServer(new Response(200, "abcd", 4));
		string path = Path.Combine(directory, "partial");
		File.WriteAllText(path, "old stale trailing bytes");
		using var stream = new NetworkFileStream(path, server.Uri);

		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);

		Assert.AreEqual(4, stream.ContentLength);
		Assert.AreEqual("abcd", File.ReadAllText(path));
	}

	[TestMethod]
	[DataRow(200, -1, null, null)]
	[DataRow(200, 4, "bytes 0-3/4", null)]
	[DataRow(206, 4, "bytes 0-3/4", "gzip")]
	public async Task Unknown_full_length_range_on_200_and_encoded_bodies_are_rejected_before_mutation(int status, long length, string? range, string? encoding)
	{
		await using var server = new LoopbackServer(new Response(status, "abcd", length, range, "\"one\"", ContentEncoding: encoding));
		string path = Path.Combine(directory, "partial");
		File.WriteAllText(path, "preserved");
		using var stream = new NetworkFileStream(path, server.Uri);
		await AssertRejectedAsync(stream);
		await Assert.ThrowsAsync<InvalidDataException>(() => stream.DownloadTask!.WaitAsync(Timeout));
		Assert.AreEqual("preserved", File.ReadAllText(path));
		Assert.AreEqual(0, stream.WritePosition);
		using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
	}

	[TestMethod]
	public async Task An_explicit_empty_full_response_is_an_empty_stream()
	{
		await using var server = new LoopbackServer(new Response(200, "", 0));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);
		Assert.AreEqual(0, stream.ContentLength);
		Assert.AreEqual(0, stream.WritePosition);
		Assert.AreEqual(0, stream.Read(new byte[1], 0, 1));
	}

	[TestMethod]
	[DataRow(200)]
	[DataRow(416)]
	public async Task Resume_does_not_append_a_full_response_or_treat_416_as_completion(int status)
	{
		await using var server = new LoopbackServer(new Response(status, "WXYZ", 4));
		using var stream = Resume(server.Uri, "ab", 4, "\"one\"");

		await AssertRejectedAsync(stream);

		Assert.AreEqual(2, stream.WritePosition);
		Assert.AreEqual("ab", File.ReadAllText(stream.SaveFilePath));
	}

	[TestMethod]
	public async Task Short_body_faults_the_download_and_reaches_readers_as_the_original_failure()
	{
		await using var server = new LoopbackServer(new Response(206, "ab", 4, "bytes 0-3/4", "\"one\""));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);

		var failure = await Assert.ThrowsAsync<IOException>(() => stream.DownloadTask!.WaitAsync(Timeout));
		var readerFailure = Assert.Throws<IOException>(() => stream.Read(new byte[4], 0, 4));

		Assert.AreSame(failure, readerFailure);
		Assert.IsLessThan(4, stream.WritePosition);
		stream.Dispose();
		stream.Dispose();
	}

	[TestMethod]
	public async Task Reader_never_exposes_stale_bytes_beyond_the_validated_length()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 0-3/4", "abcd", "\"one\""));
		string path = Path.Combine(directory, "partial");
		File.WriteAllText(path, "old bytes extending past the object");
		using var stream = new NetworkFileStream(path, server.Uri);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);
		byte[] buffer = new byte[40];

		int read = stream.Read(buffer, 0, buffer.Length);

		Assert.AreEqual(4, read);
		Assert.AreEqual("abcd", Encoding.ASCII.GetString(buffer, 0, read));
		Assert.AreEqual(0, stream.Read(buffer, 0, buffer.Length));
	}

	[TestMethod]
	public async Task A_changed_total_is_rejected_before_a_resumed_tail_is_truncated()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 2-5/6", "cdef", "\"one\""));
		using var stream = Resume(server.Uri, "ab", 4, "\"one\"");
		File.AppendAllText(stream.SaveFilePath, "uncommitted-tail");
		await AssertRejectedAsync(stream);
		Assert.AreEqual("abuncommitted-tail", File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(2, stream.WritePosition);
	}

	[TestMethod]
	[DataRow(null)]
	[DataRow("W/\"one\"")]
	public async Task A_bounded_first_response_needs_a_strong_validator_before_mutation(string? tag)
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 0-1/4", "ab", tag));
		string path = Path.Combine(directory, "partial");
		File.WriteAllText(path, "preserved");
		using var stream = new NetworkFileStream(path, server.Uri);
		await AssertRejectedAsync(stream);
		Assert.AreEqual("preserved", File.ReadAllText(path));
	}

	[TestMethod]
	[DataRow("query")]
	[DataRow("path")]
	[DataRow("host")]
	[DataRow("content")]
	[DataRow("missing")]
	public async Task Changed_or_missing_resource_identity_preserves_old_bytes_without_a_request(string change)
	{
		await using var server = new LoopbackServer();
		using var stream = Resume(server.Uri, "ab", 4, "\"one\"", downloadIdentity: change == "content" ? "edition-1" : null,
			missingIdentity: change == "missing");
		File.AppendAllText(stream.SaveFilePath, "tail");
		var changed = change switch
		{
			"query" => new Uri(server.Uri + "?renewed=yes"),
			"path" => new Uri(server.Uri, "/other/fixture.aaxc"),
			"host" => new UriBuilder(server.Uri) { Host = "localhost" }.Uri,
			_ => server.Uri
		};
		stream.SetUriForSameFile(changed, change == "content" ? "edition-2" : null);
		await AssertRejectedAsync(stream);
		Assert.AreEqual(0, server.Requests.Count);
		Assert.AreEqual("abtail", File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(2, stream.WritePosition);
	}

	[TestMethod]
	public async Task Serialized_identity_allows_a_renewed_query_only_for_the_same_content_and_path()
	{
		await using var server = new LoopbackServer(
			Response.Partial("bytes 0-2/6", "abc", "\"one\""),
			new Response(503, "unavailable", 11),
			Response.Partial("bytes 3-5/6", "def", "\"one\""));
		string json;
		using (var first = new NetworkFileStream(Path.Combine(directory, "partial"), new Uri(server.Uri + "?token=old"), downloadIdentity: "synthetic-content-reference"))
		{
			await first.BeginDownloadingAsync().WaitAsync(Timeout);
			await Assert.ThrowsAsync<WebException>(() => first.DownloadTask!.WaitAsync(Timeout));
			Assert.AreEqual(3, first.WritePosition);
			json = JsonConvert.SerializeObject(first);
			Assert.IsFalse(json.Contains("synthetic-content-reference", StringComparison.Ordinal));
		}
		using var resumed = JsonConvert.DeserializeObject<NetworkFileStream>(json)!;
		resumed.SetUriForSameFile(new Uri(server.Uri + "?token=new"), "synthetic-content-reference");
		await resumed.BeginDownloadingAsync().WaitAsync(Timeout);
		await resumed.DownloadTask!.WaitAsync(Timeout);
		Assert.AreEqual("abcdef", File.ReadAllText(resumed.SaveFilePath));
		Assert.AreEqual(3, server.Requests.Count);
		Assert.AreEqual("\"one\"", server.Requests.Last()["If-Range"]);
		StringAssert.Contains(server.Requests.Last()["Request-Line"], "?token=new");
	}

	[TestMethod]
	public async Task Persisted_range_and_encoding_headers_cannot_override_the_admitted_request()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 0-1/4", "ab", "\"one\""), Response.Partial("bytes 2-3/4", "cd", "\"one\""));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri, requestHeaders: new()
			{ ["rAnGe"] = "bytes=77-", ["iF-rAnGe"] = "\"wrong\"", ["aCcEpT-EnCoDiNg"] = "gzip" });
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);
		Assert.AreEqual("bytes=0-", server.Requests.First()["Range"]);
		Assert.IsFalse(server.Requests.First().ContainsKey("If-Range"));
		Assert.AreEqual("identity", server.Requests.First()["Accept-Encoding"]);
		Assert.AreEqual("bytes=2-", server.Requests.Last()["Range"]);
		Assert.AreEqual("\"one\"", server.Requests.Last()["If-Range"]);
	}

	[TestMethod]
	[DataRow("abcd", true)]
	[DataRow("abcde", false)]
	public async Task A_chunked_range_must_end_at_its_exact_advertised_extent(string body, bool valid)
	{
		await using var server = new LoopbackServer(new Response(206, body, -1, "bytes 0-3/4", "\"one\"", Chunked: true));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		if (valid)
		{
			await stream.DownloadTask!.WaitAsync(Timeout);
			Assert.AreEqual("abcd", File.ReadAllText(stream.SaveFilePath));
			Assert.AreEqual(4, stream.WritePosition);
		}
		else
		{
			var failure = await Assert.ThrowsAsync<InvalidDataException>(() => stream.DownloadTask!.WaitAsync(Timeout));
			Assert.AreSame(failure, Assert.Throws<InvalidDataException>(() => stream.Read(new byte[4], 0, 4)));
			Assert.AreEqual(0, stream.WritePosition);
			Assert.AreEqual(0, new FileInfo(stream.SaveFilePath).Length);
		}
	}

	[TestMethod]
	public async Task An_interrupted_large_response_retries_only_from_its_flushed_position()
	{
		const int MiB = 1024 * 1024;
		string expected = new string('a', MiB) + new string('b', MiB);
		await using var server = new LoopbackServer(
			new Response(206, expected[..(MiB + 1000)], expected.Length, $"bytes 0-{expected.Length - 1}/{expected.Length}", "\"one\""),
			Response.Partial($"bytes {MiB}-{expected.Length - 1}/{expected.Length}", expected[MiB..], "\"one\""));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);
		Assert.AreEqual(2, server.Requests.Count);
		Assert.AreEqual($"bytes={MiB}-", server.Requests.Last()["Range"]);
		Assert.AreEqual("\"one\"", server.Requests.Last()["If-Range"]);
		Assert.AreEqual(expected, File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(expected.Length, stream.WritePosition);
	}

	[TestMethod]
	public async Task A_later_range_cannot_change_the_total_after_an_earlier_block_commits()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 0-2/6", "abc", "\"one\""), Response.Partial("bytes 3-5/7", "def", "\"one\""));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		await stream.BeginDownloadingAsync().WaitAsync(Timeout);
		await Assert.ThrowsAsync<InvalidDataException>(() => stream.DownloadTask!.WaitAsync(Timeout));
		Assert.AreEqual(3, stream.WritePosition);
		Assert.AreEqual("abc", File.ReadAllText(stream.SaveFilePath));
	}

	[TestMethod]
	public async Task Concurrent_begin_calls_share_one_producer()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 0-3/4", "abcd", "\"one\""));
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		var begins = Enumerable.Range(0, 12).Select(_ => stream.BeginDownloadingAsync()).ToArray();
		Assert.IsTrue(begins.All(task => ReferenceEquals(task, begins[0])));
		await Task.WhenAll(begins).WaitAsync(Timeout);
		await stream.DownloadTask!.WaitAsync(Timeout);
		Assert.AreEqual(1, server.Requests.Count);
	}

	[TestMethod]
	[DataRow(false)]
	[DataRow(true)]
	public async Task Disposal_during_first_request_or_body_closes_the_peer_and_allows_no_second_producer(bool sendHeaders)
	{
		var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var server = new LoopbackServer(async (_, _, network, token) =>
		{
			if (sendHeaders) await WriteResponseAsync(network, Response.Partial("bytes 0-3/4", "abcd", "\"one\""), token, includeBody: false);
			requested.TrySetResult();
			await WaitForPeerCloseAsync(network, token);
			closed.TrySetResult();
		});
		string path = Path.Combine(directory, "partial");
		using var stream = new NetworkFileStream(path, server.Uri);
		Task first = stream.BeginDownloadingAsync();
		await requested.Task.WaitAsync(Timeout);
		if (sendHeaders) await first.WaitAsync(Timeout);
		var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
		{
			try { await stream.BeginDownloadingAsync(); }
			catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
		})).ToArray();
		await Task.Run(stream.Dispose).WaitAsync(Timeout);
		await Task.WhenAll(callers).WaitAsync(Timeout);
		if (!sendHeaders) await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
		await closed.Task.WaitAsync(Timeout);
		Assert.IsTrue(stream.DownloadTask!.IsCompleted);
		Assert.AreEqual(1, server.Requests.Count);
		Assert.AreEqual(0, stream.WritePosition);
		using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
	}

	[TestMethod]
	public async Task Rejected_first_headers_close_the_unread_body_before_caller_disposal()
	{
		var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var server = new LoopbackServer(async (_, _, network, token) =>
		{
			await WriteResponseAsync(network, new Response(206, "unused", 100, "bytes 1-100/101", "\"one\""), token, includeBody: false);
			await WaitForPeerCloseAsync(network, token);
			closed.TrySetResult();
		});
		string path = Path.Combine(directory, "partial");
		File.WriteAllText(path, "old partial");
		using var stream = new NetworkFileStream(path, server.Uri);
		await AssertRejectedAsync(stream);
		await closed.Task.WaitAsync(Timeout);
		Assert.AreEqual("old partial", File.ReadAllText(path));
		await Assert.ThrowsAsync<InvalidDataException>(() => stream.DownloadTask!.WaitAsync(Timeout));
		using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
	}

	[TestMethod]
	public async Task The_audiobook_owner_preserves_mismatched_cache_and_closes_it_on_begin_failure()
	{
		await using var server = new LoopbackServer();
		string jsonPath = Path.Combine(directory, "fixture.json");
		string audioPath = Path.Combine(directory, "fixture.aaxc");
		using (var saved = Resume(server.Uri, "ab", 4, "\"one\"", "edition-1", cacheName: "fixture.aaxc"))
		{
			File.AppendAllText(audioPath, "tail");
			File.WriteAllText(jsonPath, JsonConvert.SerializeObject(saved));
		}
		var owner = new ProbeDownloader(directory, new FixtureOptions(server.Uri, "edition-2"));
		await Assert.ThrowsAsync<InvalidDataException>(() => owner.RunAsync().WaitAsync(Timeout));
		Assert.AreEqual("abtail", File.ReadAllText(audioPath));
		Assert.IsTrue(File.Exists(jsonPath));
		Assert.AreEqual(0, server.Requests.Count);
		using var exclusive = File.Open(audioPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
	}

	[TestMethod]
	[DataRow("query", true)]
	[DataRow("acr", false)]
	[DataRow("codec", false)]
	[DataRow("version", false)]
	public void Download_options_bind_content_facts_while_allowing_signed_query_renewal(string change, bool same)
	{
		WithMockConfiguration(() =>
		{
			using var first = CreateOptions("https://fixture.invalid/audio?signature=old");
			using var second = CreateOptions("https://fixture.invalid/audio?signature=new",
				acr: change == "acr" ? "another-acr" : "fixture-acr",
				codec: change == "codec" ? "ec-3" : "ac-4",
				version: change == "version" ? "2" : "1");
			Assert.IsNotNull(first.DownloadIdentity);
			Assert.AreEqual(same, first.DownloadIdentity == second.DownloadIdentity);
			Assert.IsFalse(first.DownloadIdentity.Contains("signature", StringComparison.Ordinal));
		});
	}

	[TestMethod]
	public void Incomplete_content_reference_uses_the_exact_url_fallback()
		=> WithMockConfiguration(() =>
		{
			using var options = CreateOptions("https://fixture.invalid/audio?signature=old", acr: "");
			Assert.IsNull(options.DownloadIdentity);
		});

	private void WithMockConfiguration(Action action)
	{
		string? previous = Environment.GetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR);
		Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, directory);
		Configuration.CreateMockInstance();
		try
		{
			AudibleUtilities.AudibleApiStorage.EnsureAccountsSettingsFileExists();
			action();
		}
		finally
		{
			Configuration.RestoreSingletonInstance();
			Environment.SetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR, previous);
		}
	}

	private static DownloadOptions CreateOptions(string url, string acr = "fixture-acr", string codec = "ac-4", string version = "1")
	{
		var book = new Book(new AudibleProductId("B0HTTPTEST"), "HTTP identity fixture", "", "", 1,
			DataLayer.ContentType.Product, [new Contributor("Author")], [new Contributor("Narrator")], "us");
		var libraryBook = new LibraryBook(book, new DateTime(2026, 1, 1), "test-account");
		var license = DownloadOptions.LicenseInfo.Create(new ContentLicense
		{
			DrmType = DrmType.Widevine,
			ContentMetadata = new()
			{
				ContentReference = new() { Acr = acr, Asin = "B0HTTPTEST", Codec = codec, ContentFormat = "MPEG4", Marketplace = "us", Sku = "fixture", Tempo = "1", Version = version },
				ContentUrl = new() { OfflineUrl = url },
				ChapterInfo = new() { RuntimeLengthMs = 1000, Chapters = [new Chapter { Title = "Chapter 1", LengthMs = 1000 }] }
			}
		});
		return DownloadOptions.BuildDownloadOptions(libraryBook, Configuration.Instance, license);
	}

	private sealed class ProbeDownloader(string directory, IDownloadOptions options) : AudiobookDownloadBase(directory, directory, options)
	{
		protected override Task<bool> Step_DownloadAndDecryptAudiobookAsync() => Task.FromResult(true);
	}

	private sealed class FixtureOptions(Uri uri, string identity) : IDownloadOptions
	{
		public event EventHandler<long> DownloadSpeedChanged { add { } remove { } }
		public string DownloadUrl => uri.AbsoluteUri;
		public string? DownloadIdentity => identity;
		public string UserAgent => "Hermetic-HTTP-fixture";
		public string? AudibleProductId => "fixture";
		public string? Title => "Fixture";
		public string? Subtitle => null;
		public string? Publisher => null;
		public string? Language => null;
		public string? SeriesName => null;
		public string? SeriesNumber => null;
		public KeyData[]? DecryptionKeys => null;
		public TimeSpan RuntimeLength => TimeSpan.FromSeconds(1);
		public OutputFormat OutputFormat => OutputFormat.M4b;
		public bool StripUnabridged => false;
		public bool CreateCueSheet => false;
		public long DownloadSpeedBps => 0;
		public Mpeg4Lib.ChapterInfo ChapterInfo { get; } = new(TimeSpan.Zero);
		public bool FixupFile => false;
		public NAudio.Lame.LameConfig? LameConfig => null;
		public bool Downsample => false;
		public bool MatchSourceBitrate => false;
		public bool MoveMoovToBeginning => false;
		public string GetMultipartTitle(MultiConvertFileProperties props) => "Fixture";
		public AAXClean.FileType? InputType => AAXClean.FileType.Aaxc;
	}

	private static async Task AssertRejectedAsync(NetworkFileStream stream)
	{
		Exception? failure = null;
		try { await stream.BeginDownloadingAsync().WaitAsync(Timeout); }
		catch (Exception ex) { failure = ex; }
		Assert.IsTrue(failure is IOException or InvalidDataException or WebException or HttpRequestException,
			"Expected a prompt HTTP/data rejection, not success or a timeout. Actual: " + failure);
	}

	private NetworkFileStream Resume(Uri uri, string prefix, long totalLength, string? entityTag, string? downloadIdentity = null, bool missingIdentity = false, string cacheName = "partial")
	{
		string path = Path.Combine(directory, cacheName);
		File.WriteAllText(path, prefix);
		string identity;
		using (var fresh = new NetworkFileStream(path, uri, downloadIdentity: downloadIdentity))
			identity = fresh.ResourceIdentity!;
		var saved = new JObject
		{
			["SaveFilePath"] = path,
			["Uri"] = uri.AbsoluteUri,
			["RequestHeaders"] = new JObject(),
			["WritePosition"] = prefix.Length,
			["ContentLength"] = totalLength,
			["EntityTag"] = entityTag,
			["ResourceIdentity"] = missingIdentity ? null : identity,
			["EffectiveResourceIdentity"] = identity
		};
		var result = JsonConvert.DeserializeObject<NetworkFileStream>(saved.ToString())!;
		if (downloadIdentity is not null) result.SetUriForSameFile(uri, downloadIdentity);
		return result;
	}

	[TestMethod]
	[DataRow("same", true, true)]
	[DataRow("path", true, false)]
	[DataRow("host", true, false)]
	[DataRow("query", true, true)]
	[DataRow("query", false, false)]
	public async Task Redirected_resume_binds_the_final_resource_even_when_the_ETag_is_reused(string change, bool explicitIdentity, bool accepted)
	{
		Uri? finalUri = null;
		await using var server = new LoopbackServer((index, _, network, token) =>
		{
			var response = index switch
			{
				0 => new Response(302, "", 0, Location: finalUri!.AbsoluteUri),
				1 => Response.Partial("bytes 0-2/6", "abc", "\"same-tag\""),
				2 => new Response(503, "unavailable", 11),
				3 => new Response(302, "", 0, Location: finalUri!.AbsoluteUri),
				4 => Response.Partial("bytes 3-5/6", "def", "\"same-tag\""),
				_ => new Response(500, "unexpected", 10)
			};
			return WriteResponseAsync(network, response, token);
		});
		finalUri = new Uri(server.Uri, "/selected/book.aaxc?signature=one");
		string? identity = explicitIdentity ? "selected-content-v1" : null;
		string saved;
		string path = Path.Combine(directory, "redirected");
		using (var initial = new NetworkFileStream(path, server.Uri, downloadIdentity: identity))
		{
			await initial.BeginDownloadingAsync().WaitAsync(Timeout);
			await Assert.ThrowsAsync<WebException>(() => initial.DownloadTask!.WaitAsync(Timeout));
			Assert.AreEqual(3, initial.WritePosition);
			saved = JsonConvert.SerializeObject(initial);
		}
		finalUri = change switch
		{
			"path" => new Uri(server.Uri, "/different/book.aaxc?signature=one"),
			"host" => new UriBuilder(finalUri) { Host = "localhost" }.Uri,
			"query" => new UriBuilder(finalUri) { Query = "signature=renewed" }.Uri,
			_ => finalUri
		};
		using var resumed = JsonConvert.DeserializeObject<NetworkFileStream>(saved)!;
		resumed.SetUriForSameFile(server.Uri, identity);
		if (accepted)
		{
			await resumed.BeginDownloadingAsync().WaitAsync(Timeout);
			await resumed.DownloadTask!.WaitAsync(Timeout);
			Assert.AreEqual("abcdef", File.ReadAllText(path));
			Assert.AreEqual(6, resumed.WritePosition);
		}
		else
		{
			await AssertRejectedAsync(resumed);
			Assert.AreEqual("abc", File.ReadAllText(path));
			Assert.AreEqual(3, resumed.WritePosition);
		}
		Assert.AreEqual(5, server.Requests.Count);
		Assert.AreEqual("\"same-tag\"", server.Requests.Last()["If-Range"]);
	}

	[TestMethod]
	public async Task A_saved_partial_without_final_resource_identity_is_retained_without_requesting()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 2-3/4", "cd", "\"one\""));
		JObject saved;
		using (var initial = Resume(server.Uri, "ab", 4, "\"one\""))
			saved = JObject.Parse(JsonConvert.SerializeObject(initial));
		saved.Remove("EffectiveResourceIdentity");
		using var stream = JsonConvert.DeserializeObject<NetworkFileStream>(saved.ToString())!;
		await AssertRejectedAsync(stream);
		Assert.AreEqual("ab", File.ReadAllText(stream.SaveFilePath));
		Assert.AreEqual(0, server.Requests.Count);
	}

	[TestMethod]
	public async Task Private_persisted_partial_reopens_and_finishes_against_the_same_validator()
	{
		await using var server = new LoopbackServer(Response.Partial("bytes 2-3/4", "cd", "\"one\""));
		string state = Path.Combine(directory, "resume.json");
		using (var initial = Resume(server.Uri, "ab", 4, "\"one\""))
		using (var saved = new NetworkFileStreamPersister(initial, state)) { }
		using (var restored = new NetworkFileStreamPersister(state))
		{
			await restored.NetworkFileStream.BeginDownloadingAsync().WaitAsync(Timeout);
			await restored.NetworkFileStream.DownloadTask!.WaitAsync(Timeout);
		}
		using var completed = new NetworkFileStreamPersister(state);
		Assert.AreEqual(4, completed.NetworkFileStream.WritePosition);
		Assert.AreEqual("abcd", File.ReadAllText(completed.NetworkFileStream.SaveFilePath));
		Assert.AreEqual("\"one\"", server.Requests.Single()["If-Range"]);
		if (!OperatingSystem.IsWindows())
			Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(state));
	}

	[TestMethod]
	public async Task Checkpoint_failure_does_not_replace_the_original_download_failure()
	{
		await using var server = new LoopbackServer(new Response(403, "no", 2));
		string state = Path.Combine(directory, "resume.json");
		using var stream = new NetworkFileStream(Path.Combine(directory, "partial"), server.Uri);
		using var persister = new NetworkFileStreamPersister(stream, state);
		File.Delete(state);
		Directory.CreateDirectory(state); // Inject publication failure at the final checkpoint.
		var failure = await Assert.ThrowsAsync<WebException>(() => stream.BeginDownloadingAsync().WaitAsync(Timeout));
		var taskFailure = await Assert.ThrowsAsync<WebException>(() => stream.DownloadTask!.WaitAsync(Timeout));
		Assert.AreSame(failure, taskFailure);
		Assert.AreSame(failure, Assert.Throws<WebException>(() => stream.Read(new byte[1], 0, 1)));
		persister.Dispose();
		using var exclusive = File.Open(stream.SaveFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		Assert.AreEqual(1, Directory.GetFiles(directory).Length);
	}

	private sealed record Response(int Status, string Body, long DeclaredLength, string? Range = null, string? Etag = null, bool Chunked = false, string? Location = null, string? ContentEncoding = null)
	{
		public static Response Partial(string range, string body, string? etag = null)
			=> new(206, body, Encoding.ASCII.GetByteCount(body), range, etag);
	}

	private static async Task WriteResponseAsync(NetworkStream network, Response response, CancellationToken token, bool includeBody = true)
	{
		string head = $"HTTP/1.1 {response.Status} Fixture\r\nConnection: close\r\n";
		if (response.Chunked) head += "Transfer-Encoding: chunked\r\n";
		else if (response.DeclaredLength >= 0) head += $"Content-Length: {response.DeclaredLength}\r\n";
		if (response.Range is not null) head += $"Content-Range: {response.Range}\r\n";
		if (response.Etag is not null) head += $"ETag: {response.Etag}\r\n";
		if (response.Location is not null) head += $"Location: {response.Location}\r\n";
		if (response.ContentEncoding is not null) head += $"Content-Encoding: {response.ContentEncoding}\r\n";
		string body = includeBody ? response.Body : "";
		if (includeBody && response.Chunked) body = $"{Encoding.ASCII.GetByteCount(body):x}\r\n{body}\r\n0\r\n\r\n";
		await network.WriteAsync(Encoding.ASCII.GetBytes(head + "\r\n" + body), token);
	}

	private static async Task WaitForPeerCloseAsync(NetworkStream network, CancellationToken token)
	{
		try { Assert.AreEqual(0, await network.ReadAsync(new byte[1], token)); }
		catch (IOException) { /* Cancellation can reset the socket instead of returning EOF. */ }
	}

	private sealed class LoopbackServer : IAsyncDisposable
	{
		private readonly TcpListener listener = new(IPAddress.Loopback, 0);
		private readonly CancellationTokenSource cancellation = new();
		private readonly Task worker;
		public Uri Uri { get; }
		public ConcurrentQueue<Dictionary<string, string>> Requests { get; } = new();

		public LoopbackServer(params Response[] responses)
			: this((index, _, network, token) => WriteResponseAsync(network,
				index < responses.Length ? responses[index] : new Response(500, "unexpected request", 18), token)) { }

		public LoopbackServer(Func<int, Dictionary<string, string>, NetworkStream, CancellationToken, Task> respond)
		{
			listener.Start();
			Uri = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/fixture.aaxc");
			worker = Task.Run(async () =>
			{
				try
				{
					int index = 0;
					while (!cancellation.IsCancellationRequested)
					{
						using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
						using var network = client.GetStream();
						using var reader = new StreamReader(network, Encoding.ASCII, leaveOpen: true);
						var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
						headers["Request-Line"] = await reader.ReadLineAsync(cancellation.Token) ?? "";
						while (await reader.ReadLineAsync(cancellation.Token) is { Length: > 0 } line)
						{
							int colon = line.IndexOf(':');
							if (colon > 0) headers[line[..colon]] = line[(colon + 1)..].Trim();
						}
						Requests.Enqueue(headers);
						await respond(index++, headers, network, cancellation.Token);
					}
				}
				catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
				catch (SocketException) when (cancellation.IsCancellationRequested) { }
			});
		}

		public async ValueTask DisposeAsync()
		{
			cancellation.Cancel();
			listener.Stop();
			await worker.WaitAsync(Timeout);
			cancellation.Dispose();
		}
	}
}
