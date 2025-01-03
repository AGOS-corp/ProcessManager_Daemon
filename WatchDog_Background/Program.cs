using System;
using WatchDog_Background.ProcessManager;

namespace WatchDog_Background
{
    internal class Program
    {
        [MTAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("WatchDog TCP 서버 초기화 중...");

            // WatchDog 프로세스 매니저 초기화
            ProcessManage processManage = ProcessManage.GetInstance();

            // TCP 서버 시작
            const int serverPort = 8000;
            TcpServer.TcpServer tcpServer = new TcpServer.TcpServer(serverPort);
            tcpServer.Start();
            processManage.LoadProcessesFromJson();
            // WatchDog 프로세스 모니터링 시작
            processManage.StartMonitoring();

            Console.WriteLine($"WatchDog 서버가 {serverPort} 포트에서 실행 중입니다.");

            // 서버 실행 유지 (종료 명령 대기)
            Console.WriteLine("종료하려면 'exit'를 입력하세요.");
            while (true)
            {
                string input = Console.ReadLine();
                if (input?.Trim().ToLower() == "exit")
                {
                    break;
                }
            }

            // 종료 시 리소스 정리
            processManage.StopMonitoring();
            Console.WriteLine("WatchDog 서버가 종료되었습니다.");
        }
    }
}