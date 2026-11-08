public interface ITextLogSink {
  void Log(string message);
}

public interface ITextLog {
  void Log(string message);
}

public enum LogContext { toddler, macarena, ensemble, simulation }
