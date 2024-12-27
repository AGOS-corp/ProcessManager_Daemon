using System.Text.Json.Serialization;

namespace WatchDog_Background.Model.TcpServer
{
    public class ClientCommand
    {
        [JsonPropertyName("opcode")]
        public int Opcode { get; set; } // 명령 코드

        [JsonPropertyName("program_name")]
        public string ProgramName { get; set; } // 프로그램 이름

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; } // 파일 경로

        [JsonPropertyName("autoRestart")]
        public bool AutoRestart { get; set; } // 자동 재시작 여부

        [JsonPropertyName("restartInterval")]
        public int RestartInterval { get; set; } // 재시작 주기

        [JsonPropertyName("start_immediately")]
        public bool StartImmediately { get; set; } // 즉시 실행 여부

        [JsonPropertyName("send")]
        public bool Send { get; set; } // 실행 상태 전송 여부

        [JsonPropertyName("command")]
        public int Command { get; set; } // 실행/중지/삭제 명령
    }
}