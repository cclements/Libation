# Private resume checkpoints

NetworkFileStreamPersister owns the resume JSON and its NetworkFileStream. New
checkpoints use the same PrivateFileWriter as key sidecars: a unique adjacent
file is created with Unix mode 0600, fully written, flushed and JSON-validated,
then renamed over the destination. A failed write or validation retains the
previous checkpoint and removes only the writer's staging file. Replacement
does not follow a destination symlink for writing.

The JSON schema, URL/header values, offsets, HTTP validators, resource digests
and optional JSONPath envelope are unchanged. Legacy snapshots without identity
remain unvalidated; reopening never invents identity for existing bytes. Existing
files are read without rewriting and become private on their next checkpoint.
Downloads still own update throttling and error reporting. Disposal joins the
producer, saves its last checkpoint and detaches the event handler. Failed initial
persistence closes the newly owned stream without replacing the original error.

## Writer decision

The resolved Dinah.Core 11.0.0.1 assembly was inspected before changing the owner.
JsonFilePersister serializes and validates JSON, then calls AtomicFileWriter;
both its serialization/write method and writer selection are private. The only
virtual hooks are serializer settings and saving/saved notifications. Its
AtomicFileWriter creates temporary files with default permissions. Applying
chmod after publication would leave a disclosure window and every replacement
would reset the mode.

The internal NetworkFileStreamPersister therefore implements its small existing
resume contract directly, using the previously established private writer. It
does not introduce another store, change the external dependency, or alter other
Dinah persisters. No caller uses the former inherited transaction API. The
downloader owns one persister per operation; its updates are serialized and
disposal never holds the save lock while waiting for the producer.

## Verification and limits

Maintained FileLiberator tests cover new/replaced/staged permissions, old/current
JSON and nested documents, real loopback resume, destination symlinks, failed
validation/publication, original download errors and immediate handle release.
All values are synthetic. The initial implementation reproduced mode 0644 and
a stream left open after failed persistence; no real account data was opened.

Unix mode and symlink execution here is macOS APFS evidence. Windows still uses
inherited ACLs; no new Windows ACL guarantee is claimed. Files remain plaintext
and accessible to the same user. File flush/rename is not a power-loss or
directory-fsync guarantee. Abandonment, retention, crash-left staging cleanup
and whole-book publication journals remain separate work.
