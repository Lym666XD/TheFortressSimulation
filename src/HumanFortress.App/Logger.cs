using System.IO;

namespace HumanFortress.App
{
    /// <summary>
    /// Simple file logger that doesn't interfere with SadConsole rendering.
    /// </summary>
    public static class Logger
    {
        private static StreamWriter? _writer;
        private static readonly object _lock = new object();

        public static void Initialize(string logPath)
        {
            lock (_lock)
            {
                _writer?.Close();
                _writer = new StreamWriter(logPath, false) { AutoFlush = true };
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                _writer?.WriteLine(message);
            }
        }

        public static void Close()
        {
            lock (_lock)
            {
                _writer?.Close();
                _writer = null;
            }
        }
    }
}