using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Management.Instrumentation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WatchDog_Background
{
    public class ProcessManage
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool IsHungAppWindow(IntPtr hwnd);

        private const string WMI_QUERY = "Select * From Win32_Process";

        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private static ProcessManage Instance = null;        
        private Thread mThreadCheckProcess = null;
        private bool mIsThreadRunning = true;

        private ManagementObjectCollection oWMICollection;

        private ProcessManage() {
            GetProcessLists();
        }

        public static ProcessManage GetInstance()
        {
            if (Instance == null)
                Instance = new ProcessManage();
            return Instance;
        }


        private void GetProcessLists()
        {
            Task.Run(async() =>
            {
                using (ManagementObjectSearcher oWMI = new ManagementObjectSearcher(WMI_QUERY))
                {
                    while (this.mIsThreadRunning)
                    {
                        await semaphore.WaitAsync();
                        oWMICollection = oWMI.Get();
                        semaphore.Release();
                        Console.WriteLine($"{DateTime.Now} Get Process Lists 정상 동작 중..");

                        foreach (ManagementObject obj in oWMICollection)
                        {
                            //Process 체크
                        }
                        await Task.Delay(2000);
                        
                    }
                }
            });
            //TODO 추후 로그 처리
            Console.WriteLine("Process Lists 로직 멈춤...");
        }
    }
}
