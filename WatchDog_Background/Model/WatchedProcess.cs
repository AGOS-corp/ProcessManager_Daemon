using System;

namespace WatchDog_Background.Model
{
    public class WatchedProcess
    {
        public string FilePath { get; set; }
        public string ProcessName => System.IO.Path.GetFileName(FilePath);
        
        public string ProgramName { get; set; }
        public int ProcessId { get; set; }
        public ProcessStatus Status { get; set; }
        public bool AutoRestart { get; set; }
        public int RestartInterval { get; set; }
        public DateTime LastRunTime { get; set; }
    }
}