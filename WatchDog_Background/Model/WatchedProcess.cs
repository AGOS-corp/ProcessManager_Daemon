using System;

namespace WatchDog_Background.Model
{
    public class WatchedProcess
    {
        public string FilePath { get; set; }
        public string ProgramName { get; set; }
        public bool AutoRestart { get; set; }
        public int RestartInterval { get; set; }
        public bool StartImmediately { get; set; }
        public int ProcessId { get; set; }
        public ProcessStatus Status { get; set; }
        public DateTime LastRunTime { get; set; } = DateTime.MinValue;
        
        public bool IsManuallyStopped { get; set; }
    }

}