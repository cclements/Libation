# HTTP resume identity and admission

Status: implemented for the A3.2 HTTP integrity slice, September 7, 2026. This is the persisted network-stream contract; operation journals, archival/restart policy and resume-secret retention belong to A3.3.

## Decision

A saved prefix may be extended only when its selected resource, final response target, strong entity tag, total length and exact next byte range agree. A mismatched or insufficiently identified cache fails before append and keeps its bytes. It does not automatically delete, archive or restart the download.

`BeginDownloadingAsync` admits the initial response and returns the same task to repeated callers. `DownloadTask` represents the complete transfer. An admitted header is not completion: readers and saved progress can use only flushed bytes; the final advertised byte of a block is committed only after the HTTP body ends at that boundary.

## Additive JSON fields

Existing required `SaveFilePath`, `Uri`, `RequestHeaders`, `WritePosition` and `ContentLength` fields retain their meanings. Three optional fields are added:

| Field | Meaning |
| --- | --- |
| `EntityTag` | Strong entity tag for admitted bytes; weak, missing and wildcard tags cannot authorize a partial resume. |
| `ResourceIdentity` | SHA-256 digest binding the originally requested resource to the selected content identity. |
| `EffectiveResourceIdentity` | Equivalent digest of the final response URI after redirects; ETags from different targets cannot authorize concatenation. |

With no explicit content identity, a digest covers the exact absolute HTTP request URL, including its query. With an identity, it covers the URI scheme, authority and full path plus that identity, allowing only query renewal. Mode prefixes distinguish these two forms. These are identity digests, not hashes of downloaded content.

`DownloadOptions` supplies a nonsecret identity from DRM type, ASIN, ACR, codec, version, content format and marketplace. Missing ASIN, ACR, codec or version selects the exact-URL fallback. `IDownloadOptions.DownloadIdentity` defaults to null for existing implementations. On deserialization the caller supplies the renewed URL and current identity through `SetUriForSameFile` before beginning.

Legacy JSON remains readable but does not invent identities for existing bytes. Missing resource identity, missing final identity for nonempty caches, inconsistent position/length, or a partial cache without a strong ETag fails safely at Begin. The existing audiobook owner receives this error outside its legacy unreadable-state deletion path, retains the sidecar/cache, and closes the stream. A complete nonempty cache with matching identities and position equal to its saved length can be consumed without HTTP. Downgrading to an older application is not protected by this additive contract.

## HTTP admission

Requests own `Range`, `If-Range` and `Accept-Encoding: identity`; supplied headers cannot override them. A partial request sends the saved strong ETag through `If-Range`. There is no Last-Modified fallback.

A 206 response must contain one `bytes` range starting at the committed position, a finite positive total, and an end below that total. Content-Length, when present, must equal the range size. Its total and final resource identity must match saved state. Every resumed response must retain the same strong ETag. A bounded first response needs a strong ETag before another range can be combined. Exact bounded bodies without Content-Length are supported when HTTP framing supplies their boundary.

A fresh 200 response requires explicit Content-Length and no Content-Range. An explicit zero length is a valid empty network stream; downstream audio validation still decides whether it is an audiobook. Resumed 200, 416, unknown full length and encoded responses are rejected. Redirects remain supported, with the final target validated before any cached tail is changed.

An admitted first response permits truncation of an old uncommitted tail. Writes are flushed in bounded increments; failures roll the uncommitted tail back to the last published position. Selected connection-ending failures can retry at most five times, only after actual flushed progress and with a strong ETag; every next response is revalidated. Short or overlong bodies never publish an unverified block end. A fixed Content-Length body is bounded by HttpClient framing; this is not a raw-wire conformance parser.

## Lifecycle and proof boundary

One producer owns HTTP response/body/client and the writer. Failure captures the original exception, closes the reader, faults admission when needed and wakes blocked readers. Disposal cancels and observes the producer and releases local resources. The audiobook owner disposes its persister even when the first response fails admission.

Loopback tests cover admission, redirects, persisted identity, signed-query renewal, short and bounded bodies, retry offsets, original reader faults, cancellation before headers and during a body, repeated/concurrent Begin, owner retention and exclusive file reopening after failure. They do not establish Audible/CDN behavior, decoded audio correctness, power-loss durability, sidecar tamper resistance, output publication/recovery, privacy redaction or release delivery. `FlushAsync` publication is not a power-loss guarantee. Existing sidecars still contain URI/header material; A3.3 must address that policy separately.

Protocol references: [RFC 9110 If-Range](https://www.rfc-editor.org/rfc/rfc9110.html#section-13.1.5), [combining partial content](https://www.rfc-editor.org/rfc/rfc9110.html#section-15.3.7.3), and [Content-Range](https://www.rfc-editor.org/rfc/rfc9110.html#section-14.4).
