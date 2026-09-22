# Local presented-chapter integration candidate

This isolated branch explicitly pins
`AAXClean.Codecs 3.1.1-local.20260922.b18ebc3.preroll8148435.osxarm64`.
Its exact transitive parser is `AAXClean 3.1.1-local.20260922.8148435`.
These are private development identities, not published packages. The native
payload is verified only on macOS arm64 and remains NONFREE/UNREDISTRIBUTABLE.
Do not publish/install this graph as an official release. The active app and
separate public-3.1.0 combined candidate are unchanged.

The metadata consumer now reconciles provider chapter endpoints against the
parser's `PresentedDuration`, so encoder priming/padding excluded by an edit list
cannot extend a chapter or cause valid branding removal to be rejected. The
parser owns presentation-to-media mapping; the app does not add the edit offset.
Existing fixup/MP3/split eligibility and tag owners remain in place.

A real generated-output check exposed an adjacent parser issue: remuxing exactly
at an AAC-LC sync frame discarded decoder overlap state. The pinned parser now
retains earlier compressed audio behind the output edit for single trims and
individual chapters. Compressed packets and requested presentation are retained.

## Reproduce locally

Supply a NuGet.Config whose sources include the exact local feed and the normal
public dependencies, with an isolated `globalPackagesFolder`. No package-version
override is needed because the candidate pin is committed:

```sh
dotnet build Source/_Tests/FileLiberator.Tests/FileLiberator.Tests.csproj \
  -c Release --artifacts-path /absolute/isolated-build \
  -p:RestoreConfigFile=/absolute/NuGet.Config --disable-build-servers -m:1

dotnet /absolute/isolated-build/bin/FileLiberator.Tests/release/FileLiberator.Tests.dll \
  --filter 'TestCategory!=NativeAudioIntegration' --report-trx \
  --results-directory /absolute/unit-results
```

Generated-audio checks are a separate opt-in lane. `EncoderSignalTests` in the
matched Codecs source produces synthetic AAC and reference PCM under an explicit
`AAXCLEAN_SIGNAL_OUTPUT` directory. Set `LIBATION_SYNTHETIC_AUDIO_FIXTURES` to that
directory and `LIBATION_CHAPTER_OUTPUT` to a fresh durable output directory, then
run the same exact test DLL with `--filter 'TestCategory=NativeAudioIntegration'`.
Missing fixture variables report inconclusive rather than silently substituting
provider media or claiming execution. The test validates each input hash.

Independent verification requires FFmpeg, FFprobe and NumPy:

```sh
python3 Scripts/verify-presented-chapters.py \
  /absolute/encoder-signals /absolute/chapter-output /absolute/receipt.json
```

Six generated scenarios produce nine files: mono/stereo at 16/44.1 kHz, single
remux/trim and chapter splits, fractional endpoints and intro/outro removal.
Verification requires exact decoded sample counts, contiguous unchanged
compressed source packets and head/tail/whole-signal alignment. This is bounded
synthetic AAC-LC evidence; it does not establish HE/USAC/AC-4, provider acquisition,
installed-player behavior, accessibility, all-RID or release acceptance. Existing
source/provider metadata unit migration and general sample-group rewriting remain
separate work.
