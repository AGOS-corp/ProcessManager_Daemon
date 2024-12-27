namespace WatchDog_Background.Model
{
    public enum ProcState
    {
        Terminated, // 프로세스가 종료됨
        Running,    // 프로세스가 실행 중임
        Hang        // 프로세스가 응답하지 않음
    }

}