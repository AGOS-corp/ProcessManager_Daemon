using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WatchDog_Background.Model.TcpServer;
using WatchDog_Background.ProcessManager;

namespace WatchDog_Background.TcpServer
{
    public class TcpServer
    {
        private readonly int port;
        private readonly ProcessManage processManager;
        private readonly ConcurrentDictionary<TcpClient, bool> clientSendFlags = new ConcurrentDictionary<TcpClient, bool>();

        public TcpServer(int port)
        {
            this.port = port;
            this.processManager = ProcessManage.GetInstance();
        }

        public void Start()
        {
            Task.Run(() =>
            {
                TcpListener server = new TcpListener(IPAddress.Any, port);
                server.Start();
                Console.WriteLine($"TCP 서버가 {port} 포트에서 시작되었습니다.");

                while (true)
                {
                    var client = server.AcceptTcpClient();
                    Console.WriteLine("클라이언트 연결됨.");
                    clientSendFlags[client] = false; // 초기 상태는 전송 중지
                    Task.Run(() => HandleClient(client));
                }
            });
        }

        private async void HandleClient(TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            try
            {
                Console.WriteLine("클라이언트 요청 대기 중...");

                while (client.Connected) // 연결이 유지되는 동안 반복
                {
                    // 클라이언트 요청 읽기
                    if (stream.DataAvailable) // 스트림에 데이터가 있는지 확인
                    {
                        var buffer = new char[1024];
                        int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            Console.WriteLine("클라이언트 연결 종료 요청 감지.");
                            break; // 클라이언트가 연결을 닫은 경우 루프 종료
                        }

                        string request = new string(buffer, 0, bytesRead).Trim();
                        Console.WriteLine($"수신된 요청: {request}");

                        // JSON 파싱
                        if (!string.IsNullOrEmpty(request))
                        {
                            var command = JsonSerializer.Deserialize<ClientCommand>(request);

                            // 명령 처리
                            var response = ProcessCommand(client, command);

                            // 응답 전송
                            string jsonResponse = JsonSerializer.Serialize(response);
                            await writer.WriteLineAsync(jsonResponse);
                            Console.WriteLine($"응답 전송: {jsonResponse}");
                        }
                    }

                    // `send`가 true인 경우 상태를 주기적으로 전송
                    if (clientSendFlags[client])
                    {
                        var statuses = processManager.GetProcessStatusesForClient();
                        var jsonResponse = JsonSerializer.Serialize(new ServerResponse
                        {
                            Status = "success",
                            Data = statuses
                        });
                        await writer.WriteLineAsync(jsonResponse);
                        Console.WriteLine($"자동 전송: {jsonResponse}");
                        await Task.Delay(2000); // 2초 간격으로 상태 전송
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON 처리 중 오류: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 처리 중 오류: {ex.Message}");
            }
            finally
            {
                // 클라이언트 연결 종료
                clientSendFlags.TryRemove(client, out _);
                client.Close();
                Console.WriteLine("클라이언트 연결 종료됨.");
            }
        }

        private ServerResponse ProcessCommand(TcpClient client, ClientCommand command)
        {
            Console.WriteLine($"Opcode: {command.Opcode}");

            switch (command.Opcode)
            {
                case 1001: // 상태 조회 및 send 제어
                    Console.WriteLine("프로그램 상태 조회 요청 처리 중...");
                    clientSendFlags[client] = command.Send; // send 값 업데이트
                    return new ServerResponse
                    {
                        Status = "success",
                        Message = command.Send ? "상태 전송 시작" : "상태 전송 중지"
                    };

                case 1002: // 실행/중지/삭제
                    Console.WriteLine($"명령: {command.Command}, 프로그램 이름: {command.ProgramName}");
                    var message = processManager.HandleCommand(
                        command.ProgramName,
                        command.Command,
                        command.AutoRestart,
                        command.RestartInterval,
                        command.StartImmediately
                    );
                    return new ServerResponse { Status = "success", Message = message };

                case 1003: // 프로그램 추가
                    Console.WriteLine($"프로그램 추가 요청: {command.FilePath}");
                    bool added = processManager.AddProcess(
                        command.FilePath,
                        command.ProgramName, 
                        command.AutoRestart,
                        command.RestartInterval,
                        command.StartImmediately
                    );
                    return new ServerResponse
                    {
                        Status = added ? "success" : "failure",
                        Message = added ? "프로그램 추가 성공" : "프로그램 추가 실패"
                    };

                default:
                    Console.WriteLine("알 수 없는 명령");
                    return new ServerResponse
                    {
                        Status = "failure",
                        Message = "알 수 없는 명령"
                    };
            }
        }
    }
}
