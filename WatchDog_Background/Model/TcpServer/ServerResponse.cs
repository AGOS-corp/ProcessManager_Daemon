namespace WatchDog_Background.Model.TcpServer
{
    public class ServerResponse
    {
        public string Status { get; set; } // "success" 또는 "failure"
        public string Message { get; set; } // 응답 메시지
        public object Data { get; set; } // 추가 데이터 (옵션)
    }
}