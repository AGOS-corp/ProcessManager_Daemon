# API README

## 개요
본 문서는 TCP 통신 기반으로 설계된 서버/클라이언트 API에 대한 정의를 다룹니다. 데이터 형식은 JSON을 사용하며, 주요 기능은 **WatchDog**, **업데이트**, **설치**로 나뉩니다.

---

## API 설계

### **1. WatchDog 기능**
#### **1-1. 프로그램 실행 상태 조회**
- **설명**: 서버가 실행 중인 프로그램 상태를 클라이언트에게 전달합니다.

**요청 (Client → Server):**
```json
{
    "opcode": 1001,
    "send": true
}
```

**응답 (Server → Client):**
```json
{
    "status": "success",
    "data": [
        {
            "program_name": "ProgramA",
            "is_running": true,
            "command": 1,
            "auto_restart": true,
            "restart_interval": 60,
            "start_immediately": true
        },
        {
            "program_name": "ProgramB",
            "is_running": false
        }
    ]
}
```

---

#### **1-2. 프로그램 실행/중지/삭제**
- **설명**: 클라이언트가 서버에 프로그램 실행, 중지, 삭제를 요청합니다.

| Key               | Description                              |
|-------------------|------------------------------------------|
| `opcode`          | `1002`                                   |
| `program_name`    | 프로그램 이름                            |
| `command`         | `1` (실행), `2` (중지), `3` (삭제)       |
| `auto_restart`    | 자동 재시작 여부 (`true`/`false`)         |
| `restart_interval`| 재시작 주기 (초)                         |
| `start_immediately`| 등록 후 바로 실행 여부                  |

**요청 (Client → Server):**
```json
{
    "opcode": 1002,
    "program_name": "ProgramA",
    "command": 1,
    "auto_restart": true,
    "restart_interval": 60,
    "start_immediately": true
}
```

**응답 (Server → Client):**
```json
{
    "status": "success",
    "message": "ProgramA started successfully"
}
```

---

#### **1-3. 프로그램 리스트 추가**
- **설명**: 프로그램 리스트에 새 항목을 추가합니다.

| Key               | Description                              |
|-------------------|------------------------------------------|
| `opcode`          | `1003`                                   |
| `filePath`        | 프로그램 실행 주소                        |
| `auto_restart`    | 자동 재시작 여부 (`true`/`false`)         |
| `restart_interval`| 재시작 주기 (초)                         |
| `start_immediately`| 등록 후 바로 실행 여부                  |

**요청 (Client → Server):**
```json
{
    "opcode": 1003,
    "filePath": "D:\\AgosTest\\AGOS_Execution.exe",
    "autoRestart": true,
    "restartInterval": 60,
    "start_immediately": true
}
```

**응답 (Server → Client):**
```json
{
    "status": "success",
    "message": "프로그램 추가 성공"
}
```

---

### **2. 업데이트 기능**
#### **2-1. 서버 설치된 버전 조회**
- **설명**: 서버가 현재 설치된 프로그램 버전 정보를 클라이언트에게 전달합니다.

**요청 (Client → Server):**
```json
{
    "opcode": 2001
}
```

**응답 (Server → Client):**
```json
{
    "status": "success",
    "data": [
        {
            "program_name": "ProgramA",
            "version": "1.0.0"
        },
        {
            "program_name": "ProgramB",
            "version": "2.3.1"
        }
    ]
}
```

---

#### **2-2. 업데이트 요청**
- **설명**: 클라이언트가 특정 프로그램의 업데이트를 요청합니다.

| Key           | Description                        |
|---------------|------------------------------------|
| `opcode`      | `2002`                             |
| `program_name`| 업데이트할 프로그램 이름           |
| `new_version` | 업데이트할 버전                   |

**요청 (Client → Server):**
```json
{
    "opcode": 2002,
    "program_name": "ProgramA",
    "new_version": "1.1.0"
}
```

**응답 (Server → Client):**
```json
{
    "status": "success",
    "message": "ProgramA is being updated to version 1.1.0"
}
```

---

### **3. 설치 기능**
#### **3-1. 프로그램 설치 요청**
- **설명**: 클라이언트가 새로운 프로그램 설치를 요청합니다.

| Key               | Description                              |
|-------------------|------------------------------------------|
| `opcode`          | `3001`                                   |
| `program_name`    | 설치할 프로그램 이름                     |
| `install_path`    | 설치 경로                                |
| `start_after_install` | 설치 후 실행 여부 (`true`/`false`)     |

**요청 (Client → Server):**
```json
{
    "opcode": 3001,
    "program_name": "ProgramC",
    "install_path": "/usr/local/programC",
    "start_after_install": true
}
```

**응답 (Server → Client):**
```json
{
    "status": "success",
    "message": "ProgramC installed successfully"
}
```

---

#### **3-2. 설치 상태 확인**
- **설명**: 클라이언트가 설치 진행 상태를 요청합니다.

| Key           | Description                              |
|---------------|------------------------------------------|
| `opcode`      | `3002`                                   |
| `program_name`| 설치 상태를 확인할 프로그램 이름         |

**요청 (Client → Server):**
```json
{
    "opcode": 3002,
    "program_name": "ProgramC"
}
```

**응답 (Server → Client):**
```json
{
    "status": "in_progress",
    "progress": 60
}
```

---

## Opcode 목록
| Opcode  | 기능                        | 설명                          |
|---------|-----------------------------|-------------------------------|
| `1001`  | WatchDog 상태 조회         | 서버의 프로그램 상태 조회     |
| `1002`  | WatchDog 관리 요청         | 프로그램 실행/중지/삭제 관리 |
| `1003`  | WatchDog 프로세스 등록    | 프로그램 리스트 등록          |
| `2001`  | 업데이트 버전 조회         | 서버 설치된 버전 정보 요청    |
| `2002`  | 업데이트 요청              | 특정 프로그램 업데이트 요청  |
| `3001`  | 프로그램 설치 요청         | 새로운 프로그램 설치 요청    |
| `3002`  | 설치 진행 상태 확인        | 특정 설치 상태 확인 요청     |
| `9999`  | 프로그램 강제 종료        | 프로그램 강제종료 요청     |

---

## 에러 코드
| 에러 코드 | 설명                 | 대응 방법                      |
|-----------|----------------------|-------------------------------|
| `400`     | 잘못된 요청          | 요청 데이터를 확인하세요.      |
| `401`     | 인증 실패            | 인증 정보를 확인하세요.        |
| `404`     | 자원을 찾을 수 없음   | 요청 경로나 데이터를 확인하세요.|
| `500`     | 서버 내부 오류       | 관리자에게 문의하세요.         |

---

## 참고 사항
- 데이터 형식은 **JSON**으로 통일합니다.
- `opcode`를 통해 명령을 구분합니다.
- 클라이언트와 서버 간 통신은 **TCP**를 기반으로 합니다.

