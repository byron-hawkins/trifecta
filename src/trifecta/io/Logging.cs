/* stub: Copilot */
/* author: Byron */

namespace trifecta {
namespace io {

public interface ITextLogSink {
  void Log(string message);
  void Flush();
}

public interface ITextLog {
  void Log(string message, string path, int line, string member) {}
}

public enum LogContext { toddler, macarena, ensemble, simulation }

}
}
