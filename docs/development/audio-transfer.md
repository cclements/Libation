# Staged file transfer contract

A cross-volume or unknown-device move copies to a unique temporary file beside
the destination. Only a completely copied, flushed, closed file is renamed into
place, using the caller's existing overwrite setting. Cancellation, a stopped
progress callback, copy failure or rename conflict retains the source and the
old destination. Handled failure cleans only this operation's staging path.
Source deletion happens after successful publication. An identical-path move
retains its existing no-op behavior; ambiguous case-only aliases are rejected
in the unknown-device fallback path.

Unix device lookup invokes stat with literal arguments, bounded execution and
exit-status checks. Filenames containing quotes, dollar substitutions or backticks
are not shell commands. Unknown devices choose the staged-copy path. Unix
creation mode carries existing restricted source permissions to a copied archive
or key sidecar. Windows ACL behavior and physical cross-device execution remain
separate platform checks.

The read-ahead task is joined before a pooled buffer can be returned, including
write failure and progress cancellation. FileLiberator treats an incomplete move
as a failure before updating the output path or emitting file-created state.

Eighteen focused cases cover cancellation, callback failure, publication conflict,
old-file visibility, exact short/empty output, same-path/same-volume behavior,
unknown device identity, literal filenames and owner-only Unix modes. The prior
behavior, with only an injectable device-selection test seam added, fails thirteen
cases and passes five controls. The final full FileManager suite passes 254/254.
The copy lane is selected deterministically; it uses real files on macOS APFS and
does not claim a physical cross-volume or Windows/Linux run.

This is per-file publication, not a whole-book transaction. A process crash can
leave a staging file; source deletion failure can leave both copies. Multipart
crash/restart journals, recovery discovery, cross-volume crash durability and
user-selected archival policy remain separate A3.3 work. Existing retained keys
are still plaintext; protecting their initial creation and broader resume secrets
is a separate follow-up.
