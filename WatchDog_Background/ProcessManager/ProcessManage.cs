using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        
        private const string JsonFilePath = "processes.json"; // JSON 파일 경로 (실행 파일과 동일한 위치)
        private const string WmiQuery = "Select * From Win32_Process";
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static ProcessManage _instance;
        public Thread MThreadCheckProcess = null;
        private bool _mIsThreadRunning = true;
        private readonly List<WatchedProcess> _watchedProcesses = new List<WatchedProcess>();
        private ManagementObjectCollection _oWmiCollection;

        public static ProcessManage GetInstance()
        {
            return _instance ?? (_instance = new ProcessManage());
        }
        public List<object> GetProcessStatusesForClient()
        {
            lock (_watchedProcesses)
            {
                return _watchedProcesses.Select(p => new
                {
                    program_name = p.ProgramName,
                    is_running = p.Status == ProcessStatus.Running,
                    command = p.Status == ProcessStatus.Running ? 1 : 0,
                    auto_restart = p.AutoRestart,
                    restart_interval = p.RestartInterval,
                    start_immediately = p.LastRunTime != DateTime.MinValue
                }).ToList<object>();
            }
        }

        public bool AddProcess(string filePath, string programName, bool autoRestart = false, int restartInterval = 60,
            bool startImmediately = false)
        {
            lock (_watchedProcesses)
            {
                if (!IsValidExecutable(filePath))
                {
                    Console.WriteLine($"프로세스 추가 실패: 유효하지 않은 파일 경로 - {filePath}");
                    return false;
                }

                if (_watchedProcesses.Any(p => p.FilePath == filePath))
                {
                    Console.WriteLine($"프로세스 {filePath} 이미 추가됨.");
                    return false;
                }

                if (string.IsNullOrEmpty(programName))
                    programName = Path.GetFileName(filePath);

                var newProcess = new WatchedProcess
                {
                    FilePath = filePath,
                    ProgramName = programName,
                    AutoRestart = autoRestart,
                    RestartInterval = restartInterval,
                    LastRunTime = DateTime.MinValue
                };

                _watchedProcesses.Add(newProcess);
                Console.WriteLine($"프로세스 추가됨: {filePath}, 이름: {programName}");

                SyncProcessesToJson();

                if (startImmediately)
                {
                    Console.WriteLine($"즉시 실행 시도: {filePath}");
                    if (!StartProcess(filePath)) Console.WriteLine($"프로세스 실행 실패: {filePath}");
                }

                return true;
            }
        }

        public string HandleCommand(string programName, int command, bool autoRestart = false, int restartInterval = 60,
            bool startImmediately = false)
        {
            lock (_watchedProcesses)
            {
                var process = _watchedProcesses.FirstOrDefault(p => p.ProgramName == programName);
                if (process == null) return $"프로그램 {programName}이(가) 목록에 없습니다.";

                switch (command)
                {
                    case 1: // 실행
                        process.AutoRestart = autoRestart;
                        process.RestartInterval = restartInterval;
                        process.LastRunTime = startImmediately ? DateTime.Now : DateTime.MinValue;
                        if (StartProcess(process.FilePath)) return $"{programName} 실행 성공";
                        return $"{programName} 실행 실패";

                    case 2: // 중지
                        if (StopProcess(process.ProcessId)) return $"{programName} 중지 성공";
                        return $"{programName} 중지 실패";

                    case 3: // 삭제
                        if (StopProcess(process.ProcessId))
                        {
                            _watchedProcesses.Remove(process);
                            SyncProcessesToJson();
                            return $"{programName} 중지,삭제 성공";
                        }

                        return $"{programName} 중지,삭제 실패";

                    default:
                        return "알 수 없는 명령";
                }
            }
        }

        /// <summary>
        ///     JSON 파일에 프로세스 정보를 동기화
        /// </summary>
        private void SyncProcessesToJson()
        {
            try
            {
                var jsonData = JsonSerializer.Serialize(_watchedProcesses,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(JsonFilePath, jsonData);
                Console.WriteLine("JSON 파일 동기화 완료.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON 파일 동기화 실패: {ex.Message}");
            }
        }

        public void LoadProcessesFromJson()
        {
            try
            {
                if (!File.Exists(JsonFilePath))
                {
                    Console.WriteLine($"JSON 파일이 존재하지 않습니다: {JsonFilePath}");
                    return;
                }

                var jsonData = File.ReadAllText(JsonFilePath);
                var processList = JsonSerializer.Deserialize<List<WatchedProcess>>(jsonData);

                if (processList == null || !processList.Any())
                {
                    Console.WriteLine("JSON 파일에 프로세스 정보가 없습니다.");
                    return;
                }

                foreach (var process in processList)
                    if (AddProcess(process.FilePath, process.ProgramName, process.AutoRestart, process.RestartInterval,
                            process.StartImmediately))
                        Console.WriteLine($"JSON에서 프로세스 추가됨: {process.ProgramName}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 파싱 실패: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LoadProcessesFromJson 오류: {ex.Message}");
            }
        }


        /// <summary>
        ///     특정 프로세스 실행
        /// </summary>
        private bool StartProcess(string filePath)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    WorkingDirectory = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException()
                };

                var process = Process.Start(processInfo);

                if (process != null)
                {
                    var watchedProcess = _watchedProcesses.FirstOrDefault(p => p.FilePath == filePath);
                    if (watchedProcess != null)
                    {
                        watchedProcess.ProcessId = process.Id;
                        watchedProcess.LastRunTime = DateTime.Now;
                        watchedProcess.Status = ProcessStatus.Running;
                    }

                    Console.WriteLine($"프로세스 {filePath} 실행됨 (PID: {process.Id}).");
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
        ///     특정 프로세스 중지
        /// </summary>
        private bool StopProcess(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                process.Kill();
                process.WaitForExit();

                // ReSharper disable once InconsistentlySynchronizedField
                var watchedProcess = _watchedProcesses.FirstOrDefault(p => p.ProcessId == processId);
                if (watchedProcess != null) watchedProcess.Status = ProcessStatus.Stopped;

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
        ///     프로세스 상태 주기적 확인
        /// </summary>
        public void StartMonitoring()
        {
            _mIsThreadRunning = true;

            Task.Run(GetProcessLists);
            Console.WriteLine("프로세스 모니터링 시작됨.");
        }

        /// <summary>
        ///     프로세스 모니터링 종료
        /// </summary>
        public void StopMonitoring()
        {
            _mIsThreadRunning = false;
            Console.WriteLine("프로세스 모니터링 중단됨.");
        }

        private async void GetProcessLists()
        {
            try
            {
                using (var oWmi = new ManagementObjectSearcher(WmiQuery))
                {
                    while (_mIsThreadRunning)
                    {
                        await _semaphore.WaitAsync();
                        try
                        {
                            _oWmiCollection = oWmi.Get();
                            UpdateProcesses();
                            Console.WriteLine("프로세스 상태 업데이트 완료.");
                        }
                        finally
                        {
                            _semaphore.Release();
                        }

                        await Task.Delay(5000); // 5초 대기
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetProcessLists 오류: {ex.Message}");
            }
        }

        /// <summary>
        ///     _watchedProcesses 리스트를 업데이트
        /// </summary>
        private void UpdateProcesses()
        {
            lock (_watchedProcesses)
            {
                foreach (var process in _watchedProcesses)
                {
                    var matchingProcess = FindMatchingProcess(process.FilePath);

                    if (matchingProcess != null)
                        UpdateRunningProcess(process, matchingProcess);
                    else
                        HandleStoppedProcess(process);
                }
            }
        }

        /// <summary>
        ///     실행 중인 프로세스를 찾음
        /// </summary>
        /// <param name="filePath">프로세스 파일 경로</param>
        /// <returns>ManagementObject 또는 null</returns>
        private ManagementObject FindMatchingProcess(string filePath)
        {
            return _oWmiCollection.Cast<ManagementObject>()
                .FirstOrDefault(obj =>
                    obj["ExecutablePath"]?.ToString().Equals(filePath, StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        ///     실행 중인 프로세스 정보를 업데이트
        /// </summary>
        /// <param name="process">WatchedProcess 객체</param>
        /// <param name="matchingProcess">ManagementObject</param>
        private void UpdateRunningProcess(WatchedProcess process, ManagementObject matchingProcess)
        {
            process.Status = ProcessStatus.Running;
            process.ProcessId = Convert.ToInt32(matchingProcess["ProcessId"]);
            Console.WriteLine($"프로세스 실행 중: {process.ProgramName}, PID: {process.ProcessId}");
        }

        /// <summary>
        ///     중지된 프로세스를 처리
        /// </summary>
        /// <param name="process">WatchedProcess 객체</param>
        private void HandleStoppedProcess(WatchedProcess process)
        {
            if (process.Status == ProcessStatus.Running) Console.WriteLine($"프로세스 중단 감지: {process.ProgramName}");

            process.Status = ProcessStatus.Stopped;
            process.ProcessId = 0;

            if (process.AutoRestart && (DateTime.Now - process.LastRunTime).TotalSeconds >= process.RestartInterval)
            {
                Console.WriteLine($"자동 재시작 시도: {process.ProgramName}");
                if (StartProcess(process.FilePath))
                {
                    process.LastRunTime = DateTime.Now;
                    Console.WriteLine($"프로세스 {process.ProgramName} 자동 재시작 성공");
                }
                else
                {
                    Console.WriteLine($"프로세스 {process.ProgramName} 자동 재시작 실패");
                }
            }
        }


        private bool IsValidExecutable(string filePath)
        {
            try
            {
                // 파일 존재 확인
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"파일 경로가 존재하지 않습니다: {filePath}");
                    return false;
                }

                // 실행 파일인지 확인
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (extension != ".exe")
                {
                    Console.WriteLine($"유효하지 않은 실행 파일 형식: {filePath}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"파일 경로 확인 중 오류 발생: {ex.Message}");
                return false;
            }
        }
    }
}