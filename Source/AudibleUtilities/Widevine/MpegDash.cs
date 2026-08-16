using Mpeg4Lib.Boxes;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace AudibleUtilities.Widevine;

public class MpegDash
{
	private const string MpegDashNamespace = "urn:mpeg:dash:schema:mpd:2011";
	private const string CencNamespace = "urn:mpeg:cenc:2013";
	private const string UuidPreamble = "urn:uuid:";
	private XElement DashMpd { get; }
	private static XmlNamespaceManager NamespaceManager { get; } = new(new NameTable());
	static MpegDash()
	{
		NamespaceManager.AddNamespace("dash", MpegDashNamespace);
		NamespaceManager.AddNamespace("cenc", CencNamespace);
	}

	public MpegDash(Stream contents)
	{
		DashMpd = XElement.Load(contents);
	}

	public bool TryGetUri(Uri baseUri, [NotNullWhen(true)] out Uri? fileUri)
	{
		foreach (var baseUrl in DashMpd.XPathSelectElements("/dash:Period/dash:AdaptationSet/dash:Representation/dash:BaseURL", NamespaceManager))
		{
			try
			{
				fileUri = new Uri(baseUri, baseUrl.Value);
				return true;
			}
			catch
			{
				fileUri = null;
				return false;
			}
		}
		fileUri = null;
		return false;
	}

	public bool TryGetContent(
		Uri baseUri,
		Guid protectionSystemId,
		string? expectedCodec,
		[NotNullWhen(true)] out Uri? fileUri,
		[NotNullWhen(true)] out PsshBox? pssh)
	{
		fileUri = null;
		pssh = null;
		var dash = XNamespace.Get(MpegDashNamespace);

		foreach (var period in DashMpd.Elements(dash + "Period"))
		{
			foreach (var adaptation in period.Elements(dash + "AdaptationSet"))
			{
				foreach (var representation in adaptation.Elements(dash + "Representation"))
				{
					if (!IsAudioRepresentation(adaptation, representation, expectedCodec)
						|| !TryResolveBaseUrl(baseUri, [DashMpd, period, adaptation, representation], out var candidateUri)
						|| !TryGetPsshFrom(representation, adaptation, protectionSystemId, out var candidatePssh))
						continue;

					fileUri = candidateUri;
					pssh = candidatePssh;
					return true;
				}
			}
		}
		return false;
	}

	public bool TryGetPssh(Guid protectionSystemId, [NotNullWhen(true)] out PsshBox? pssh)
	{
		foreach (var psshEle in DashMpd.XPathSelectElements("/dash:Period/dash:AdaptationSet/dash:ContentProtection/cenc:pssh", NamespaceManager))
		{
			if (TryParsePssh(psshEle, protectionSystemId, out pssh))
				return true;
		}
		pssh = null;
		return false;
	}

	private static bool IsAudioRepresentation(XElement adaptation, XElement representation, string? expectedCodec)
	{
		var contentType = representation.Attribute("contentType")?.Value
			?? adaptation.Attribute("contentType")?.Value;
		if (!string.IsNullOrWhiteSpace(contentType)
			&& !contentType.Equals("audio", StringComparison.OrdinalIgnoreCase))
			return false;

		var mimeType = representation.Attribute("mimeType")?.Value
			?? adaptation.Attribute("mimeType")?.Value;
		if (!string.IsNullOrWhiteSpace(mimeType)
			&& !mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
			return false;

		var codecs = representation.Attribute("codecs")?.Value
			?? adaptation.Attribute("codecs")?.Value;
		return string.IsNullOrWhiteSpace(expectedCodec)
			|| (!string.IsNullOrWhiteSpace(codecs)
			&& codecs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Any(codec => codec.Equals(expectedCodec, StringComparison.OrdinalIgnoreCase)
					|| codec.StartsWith(expectedCodec + ".", StringComparison.OrdinalIgnoreCase)));
	}

	private static bool TryResolveBaseUrl(
		Uri manifestUri,
		IEnumerable<XElement> hierarchy,
		[NotNullWhen(true)] out Uri? resolved)
	{
		var dash = XNamespace.Get(MpegDashNamespace);
		var elements = hierarchy.ToArray();
		if (elements.Length == 0
			|| elements[^1].Element(dash + "BaseURL")?.Value.Trim() is not { Length: > 0 }
			|| elements.Any(element => element.Element(dash + "SegmentTemplate") is not null
				|| element.Element(dash + "SegmentList") is not null))
		{
			resolved = null;
			return false;
		}

		resolved = manifestUri;
		try
		{
			foreach (var element in elements)
			{
				if (element.Element(dash + "BaseURL")?.Value.Trim() is { Length: > 0 } value)
					resolved = new Uri(resolved, value);
			}
			return true;
		}
		catch
		{
			resolved = null;
			return false;
		}
	}

	private static bool TryGetPsshFrom(
		XElement representation,
		XElement adaptation,
		Guid protectionSystemId,
		[NotNullWhen(true)] out PsshBox? pssh)
	{
		if (TryGetPsshFromElement(representation, protectionSystemId, out pssh, out var representationDeclaresPssh))
			return true;
		if (representationDeclaresPssh)
			return false;

		return TryGetPsshFromElement(adaptation, protectionSystemId, out pssh, out _);
	}

	private static bool TryGetPsshFromElement(
		XElement element,
		Guid protectionSystemId,
		[NotNullWhen(true)] out PsshBox? pssh,
		out bool declaresPssh)
	{
		var dash = XNamespace.Get(MpegDashNamespace);
		var cenc = XNamespace.Get(CencNamespace);
		foreach (var protection in element.Elements(dash + "ContentProtection"))
		{
			if (!ProtectionMatches(protection, protectionSystemId))
				continue;

			declaresPssh = true;
			return TryParsePssh(protection.Element(cenc + "pssh"), protectionSystemId, out pssh);
		}

		declaresPssh = false;
		pssh = null;
		return false;
	}

	private static bool ProtectionMatches(XElement protection, Guid protectionSystemId)
		=> protection.Attribute("schemeIdUri")?.Value is string scheme
		&& scheme.Equals(UuidPreamble + protectionSystemId, StringComparison.OrdinalIgnoreCase);

	private static bool TryParsePssh(
		XElement? psshElement,
		Guid protectionSystemId,
		[NotNullWhen(true)] out PsshBox? pssh)
	{
		pssh = null;
		if (psshElement?.Value.Trim() is not { Length: > 0 } psshString
			|| psshElement.Parent is not XElement protection
			|| !ProtectionMatches(protection, protectionSystemId))
			return false;

		byte[] buffer;
		try
		{
			buffer = Convert.FromBase64String(psshString);
		}
		catch (FormatException)
		{
			return false;
		}

		if (!IsSupportedPssh(buffer, protectionSystemId))
			return false;

		try
		{
			using var stream = new MemoryStream(buffer, writable: false);
			if (BoxFactory.CreateBox(stream, null) is not PsshBox candidate)
				return false;
			if (stream.Position != stream.Length
				|| candidate.ProtectionSystemId != protectionSystemId)
			{
				candidate.Dispose();
				return false;
			}
			pssh = candidate;
			return true;
		}
		catch (InvalidDataException)
		{
			return false;
		}
		catch (EndOfStreamException)
		{
			return false;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (NotSupportedException)
		{
			return false;
		}
		catch (OverflowException)
		{
			return false;
		}
	}

	private static bool IsSupportedPssh(ReadOnlySpan<byte> box, Guid protectionSystemId)
	{
		// TEMPORARY (AAXClean/Mpeg4Lib 3.1.0): the published parser accepts trailing
		// PSSH data and does not parse version-1 KIDs. Keep this raw version-0 preflight until
		// Libation consumes a strict parser that preserves the complete supported PSSH shape.
		if (box.Length < 8 || !box[4..8].SequenceEqual("pssh"u8))
			return false;

		uint compactSize = BinaryPrimitives.ReadUInt32BigEndian(box);
		int headerSize;
		ulong declaredSize;
		if (compactSize == 1)
		{
			if (box.Length < 16)
				return false;
			headerSize = 16;
			declaredSize = BinaryPrimitives.ReadUInt64BigEndian(box[8..]);
		}
		else
		{
			if (compactSize == 0)
				return false;
			headerSize = 8;
			declaredSize = compactSize;
		}

		const int fullBoxHeaderSize = 4;
		const int systemIdSize = 16;
		const int dataSizeFieldSize = 4;
		int dataOffset = headerSize + fullBoxHeaderSize + systemIdSize + dataSizeFieldSize;
		if (declaredSize != (ulong)box.Length
			|| box.Length < dataOffset
			|| box[headerSize] != 0
			|| box[headerSize + 1] != 0
			|| box[headerSize + 2] != 0
			|| box[headerSize + 3] != 0
			|| new Guid(box.Slice(headerSize + fullBoxHeaderSize, systemIdSize), bigEndian: true) != protectionSystemId)
			return false;

		uint dataSize = BinaryPrimitives.ReadUInt32BigEndian(box.Slice(dataOffset - dataSizeFieldSize, dataSizeFieldSize));
		return dataSize == (uint)(box.Length - dataOffset);
	}
}
