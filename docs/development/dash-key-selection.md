# Immutable DASH key selection

AaxcDownloadConvertBase selects the key matching the stream's default KID without
reordering or replacing entries in IDownloadOptions.DecryptionKeys. The previous
assignment to keys[0] discarded the original first key when a later entry matched;
a subsequent selection from the same license could then fail. Selection returns
the existing KID/key bytes and keeps their big-endian GUID interpretation.

The internal selector is the same path used before SetDecryptionKey and sidecar
creation. An unmatched KID, malformed KID, empty list or missing selected key
remains an error. It introduces no new key source, fallback or retention policy.
Existing license validation and the parser still own content-key admission.

Eight synthetic cases cover all positions, repeated different selections from
one license and unchanged input on failure. The extracted prior selection logic
has four failures and four controls; the corrected full app suite passes 295/295,
with clean Avalonia/CLI builds. Exact audio package bytes are unchanged. This is
hermetic selection/source-wiring evidence, not a provider license/decryption run.

Parser construction and post-open failure cleanup should be reviewed separately;
this selection correction does not claim to close every failed-open lifetime path.
