using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;

namespace AaxDecrypter;

// Dinah.Core 11.0.0.1's JsonFilePersister has no overridable writer. Keep resume
// ownership here so both the staging file and published JSON are private at creation.
internal sealed class NetworkFileStreamPersister : IDisposable
{
    public NetworkFileStream Target { get; }
    public NetworkFileStream NetworkFileStream => Target;
    private readonly string path;
    private readonly string? jsonPath;
    private readonly object saveGate = new();
    private int disposed;

    public NetworkFileStreamPersister(NetworkFileStream networkFileStream, string path, string? jsonPath = null)
    {
        ArgumentNullException.ThrowIfNull(networkFileStream);
        Target = networkFileStream;
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            this.path = Path.GetFullPath(path);
            this.jsonPath = string.IsNullOrWhiteSpace(jsonPath) ? null : jsonPath.Trim();
            Save(this, EventArgs.Empty);
            Target.Updated += Save;
        }
        catch
        {
            // Ownership begins here; construction failure must not leave the input open.
            try { Target.Dispose(); }
            catch { /* Preserve the original persistence failure. */ }
            throw;
        }
    }

    public NetworkFileStreamPersister(string path, string? jsonPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
        this.jsonPath = string.IsNullOrWhiteSpace(jsonPath) ? null : jsonPath.Trim();
        var json = JToken.Parse(File.ReadAllText(this.path));
        Target = (this.jsonPath is null ? json : json.SelectToken(this.jsonPath))?.ToObject<NetworkFileStream>()
            ?? throw new FormatException("File was not in a format able to be imported");
        Target.Updated += Save;
    }

    private void Save(object? sender, EventArgs args)
    {
        lock (saveGate)
        {
            object payload = Target;
            if (jsonPath is not null)
            {
                var document = JObject.Parse(File.ReadAllText(path));
                var token = document.SelectToken(jsonPath)
                    ?? throw new JsonSerializationException("No match found at JSONPath: " + jsonPath);
                token.Replace(JObject.FromObject(Target));
                payload = document;
            }
            string contents = JsonConvert.SerializeObject(payload, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            PrivateFileWriter.WriteAllText(path, contents, staged => JToken.Parse(File.ReadAllText(staged)));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        // The stream joins its producer and raises the final checkpoint before we unsubscribe.
        // Never hold saveGate while joining: the producer may be saving its final position.
        try { Target.Dispose(); }
        finally { Target.Updated -= Save; }
    }
}
