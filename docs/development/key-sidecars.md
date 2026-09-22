# Key-sidecar write boundary

The existing .key plaintext format and RetainAaxFile setting are unchanged.
KeySidecar writes a uniquely named file beside the final sidecar, with Unix mode
0600 selected in FileStreamOptions before creation. It flushes/closes the complete
UTF-8 content before replacing the destination directory entry. A failed write
or publication preserves the preceding sidecar and cleans only its own staging
file. A destination symlink is replaced rather than followed for writing.
OnTempFileCreated still reports the final .key only after successful publication.

The specialized writer is needed to set the Unix creation mode before any key
bytes are written. Existing FileUtility owns best-effort cleanup. No alternate
key store or retention policy is introduced. Windows continues to inherit the
parent directory's ACL; this change does not claim new Windows ACL protection.

Seven synthetic filesystem tests cover new and legacy permissions, exact text,
encoding failure with/without a previous file, destination symlinks, publication
failure and missing parents. Previous File.WriteAllText behavior fails five
cases with two controls. The corrected app passes 285/285 hermetic tests and
clean Avalonia/CLI Release builds. No provider keys, accounts or private files
are test inputs. Mode and symlink execution is macOS APFS evidence; broader ACL,
other-host and physical archival-destination proof remain open.

The existing download cache and retention/archival owners remain in place. Network
resume JSON still contains URLs/headers and now uses the same private writer
([resume persistence](resume-persistence.md)); minimizing that state,
explicit abandonment/retention rules, crash-left staging files and whole-book
publication/restart journals remain separate A3.3 work. File content flush and
rename are not a power-loss or directory-fsync guarantee.
