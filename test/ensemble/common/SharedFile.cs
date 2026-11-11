// author: Copilot
using System.IO;
using System.Threading.Tasks;

namespace trifecta.test.common;

// Windows refuses to read a file that a writer holds open unless the reader
// allows sharing; the log writer stays open while tests inspect the log.
public static class SharedFile {
  public static string ReadAllText(string path) {
    using var stream =
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }

  public static async Task<string> ReadAllTextAsync(string path) {
    using var stream =
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
  }
}
