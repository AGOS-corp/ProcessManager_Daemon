using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WatchDog_Background.Model;

namespace WatchDog_Background.ProcessManager
{
    public class ProcessManage
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr hwnd);

        private const string WMI_QUERY = "Select * From Win32_Pr" + "ocess";
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static ProcessManage Instance = null;
        private Thread mThreadCheckProcess = null;
        private bool mIsThreadRunning = true;
        private List<WatchedProcess> watchedProcesses = new List<WatchedProcess>();
        private ManagementObjectCollection oWMICollection;


        private ProcessManage() { }

        public static ProcessManage GetInstance()
        {
            if (Instance == null)
                Instance = new ProcessManage();
            return Instance;
        }

        public bool AddProcess(string filePath, string programName, bool autoRestart = false, int restartInterval = 60, bool startImmediately = false)
        {
            lock (watchedProcesses)
            {
                if (watchedProcesses.Any(p => p.FilePath == filePath))
                {
                    Console.WriteLine($"프로세스 {filePath} 이미 추가됨.");
                    return false;
                }

                if (string.IsNullOrEmpty(programName))
                {
                    // ProgramName이 비어있을 경우 파일 이름에서 추출
                    programName = System.IO.Path.GetFileName(filePath);
                }

                var newProcess = new WatchedProcess
                {
                    FilePath = filePath,
                    ProgramName = programName,
                    AutoRestart = autoRestart,
                    RestartInterval = restartInterval,
                    LastRunTime = DateTime.MinValue
                };

                watchedProcesses.Add(newProcess);
                Console.WriteLine($"프로세스 추가됨: {filePath}, 이름: {programName}");

                if (startImmediately)
                {
                    Console.WriteLine($"즉시 실행 시도: {filePath}");
                    if (!StartProcess(filePath))
                    {
                        Console.WriteLine($"프로세스 실행 실패: {filePath}");
                    }
                }

                return true;
            }
        }



        public List<object> GetProcessStatusesForClient()
        {
            lock (watchedProcesses)
            {
                return watchedProcesses.Select(p => new
                {
                    program_name = p.ProcessName,
                    is_running = p.Status == ProcessStatus.Running,
                    command = p.Status == ProcessStatus.Running ? 1 : 0,
                    auto_restart = p.AutoRestart,
                    restart_interval = p.RestartInterval,
                    start_immediately = p.LastRunTime != DateTime.MinValue
                }).ToList<object>();
            }
        }


        
        public string HandleCommand(string programName, int command, bool autoRestart = false, int restartInterval = 60, bool startImmediately = false)
        {
            lock (watchedProcesses)
            {
                var process = watchedProcesses.FirstOrDefault(p => p.ProcessName == programName);
                if (process == null)
                {
                    return $"프로그램 {programName}이(가) 목록에 없습니다.";
                }

                switch (command)
                {
                    case 1: // 실행
                        process.AutoRestart = autoRestart;
                        process.RestartInterval = restartInterval;
                        process.LastRunTime = startImmediately ? DateTime.Now : DateTime.MinValue;
                        if (StartProcess(process.FilePath))
                        {
                            return $"{programName} 실행 성공";
                        }
                        return $"{programName} 실행 실패";

                    case 2: // 중지
                        if (StopProcess(process.ProcessId))
                        {
                            return $"{programName} 중지 성공";
                        }
                        return $"{programName} 중지 실패";

                    case 3: // 삭제
                        watchedProcesses.Remove(process);
                        return $"{programName} 삭제 성공";

                    default:
                        return "알 수 없는 명령";
                }
            }
        }


        /// <summary>
        /// 특정 프로세스 실행
        /// </summary>
        public bool StartProcess(string filePath)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException()
                };

                var process = System.Diagnostics.Process.Start(processInfo);

                if (process != null)
                {
                    var watchedProcess = watchedProcesses.FirstOrDefault(p => p.FilePath == filePath);
                    if (watchedProcess != null)
                    {
                        watchedProcess.ProcessId = process.Id;
                        watchedProcess.LastRunTime = DateTime.Now;
                        watchedProcess.Status = ProcessStatus.Running;
                    }

                    Console.WriteLine($"프로세스 {filePath} 실행됨.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"프로세스 실행 실패: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 특정 프로세스 중지
        /// </summary>
        public bool StopProcess(int processId)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit();

                var watchedProcess = watchedProcesses.FirstOrDefault(p => p.ProcessId == processId);
                if (watchedProcess != null)
                {
                    watchedProcess.Status = ProcessStatus.Stopped;
                }

                Console.WriteLine($"프로세스 {processId} 중지됨.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"프로세스 중지 실패: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 프로세스 상태 주기적 확인
        /// </summary>
        public void StartMonitoring()
        {
            if (!mIsThreadRunning)
            {
                mIsThreadRunning = true;
                Task.Run(GetProcessLists);
                Console.WriteLine("프로세스 모니터링 시작됨.");
            }
        }

        /// <summary>
        /// 프로세스 모니터링 종료
        /// </summary>
        public void StopMonitoring()
        {
            mIsThreadRunning = false;
            Console.WriteLine("프로세스 모니터링 중단됨.");
        }

        
        
        private async void GetProcessLists()
        {
            try
            {
                using (ManagementObjectSearcher oWMI = new ManagementObjectSearcher(WMI_QUERY))
                {
                    while (mIsThreadRunning)
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            oWMICollection = oWMI.Get();

                            lock (watchedProcesses)
                            {
                                foreach (var process in watchedProcesses)
                                {
                                    var matchingProcess = oWMICollection.Cast<ManagementObject>()
                                        .FirstOrDefault(obj => obj["ExecutablePath"]?.ToString()?.Equals(process.FilePath, StringComparison.OrdinalIgnoreCase) == true);

                                    if (matchingProcess != null)
                                    {
                                        process.Status = ProcessStatus.Running;
                                        process.ProcessId = Convert.ToInt32(matchingProcess["ProcessId"]);
                                    }
                                    else
                                    {
                                        process.Status = ProcessStatus.Stopped;
                                        process.ProcessId = 0;

                                        // 자동 재시작 로직
                                        if (process.AutoRestart)
                                        {
                                            Console.WriteLine($"자동 재시작: {process.ProcessName}");
                                            StartProcess(process.FilePath);
                                        }
                                    }
                                }
                            }

                            Console.WriteLine("프로세스 상태 업데이트 완료.");
                        }
                        finally
                        {
                            semaphore.Release();
                        }

                        await Task.Delay(5000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetProcessLists 오류: {ex.Message}");
            }
        }


    }
}
