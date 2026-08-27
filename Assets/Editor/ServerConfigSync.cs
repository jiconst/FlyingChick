// Unity 에디터 전용 -- 빌드 결과물에 포함되지 않음.
// HillyWings-Server/.env 의 SERVER_HOST, API_PORT 를 읽어
// Assets/Scripts/Network/ServerConfig.cs 를 자동 재생성함.
// 실행 시점: 에디터 시작/도메인 리로드 시 자동 실행, 또는 메뉴 HillyWings > Sync Server Config.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HillyWings.Editor
{
    [InitializeOnLoad]
    public static class ServerConfigSync
    {
        // .env 파일 경로: HillyWings-Client/Assets 기준 두 단계 위 → 형제 폴더 HillyWings-Server
        private static readonly string EnvPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../../HillyWings-Server/.env"));

        private static readonly string OutputPath =
            Path.Combine(Application.dataPath, "Scripts/Network/ServerConfig.cs");

        static ServerConfigSync() => Sync();

        [MenuItem("HillyWings/Sync Server Config")]
        public static void Sync()
        {
            string host = "localhost";
            string port = "8000";

            if (File.Exists(EnvPath))
            {
                foreach (string line in File.ReadAllLines(EnvPath))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#") || !trimmed.Contains("=")) continue;
                    int idx = trimmed.IndexOf('=');
                    string key = trimmed.Substring(0, idx).Trim();
                    string val = trimmed.Substring(idx + 1).Trim();
                    if (key == "SERVER_HOST" && val.Length > 0) host = val;
                    else if (key == "API_PORT" && val.Length > 0) port = val;
                }
            }
            else
            {
                Debug.LogWarning($"[ServerConfigSync] .env 파일을 찾을 수 없음: {EnvPath} — 기본값(localhost:8000) 사용");
            }

            string baseUrl = $"http://{host}:{port}";
            string content =
$@"// 자동 생성 파일 -- 직접 수정하지 말 것.
// HillyWings-Server/.env 의 SERVER_HOST, API_PORT 값에서 생성됨.
// 갱신 방법: Unity 에디터 재시작 또는 메뉴 HillyWings > Sync Server Config
namespace HillyWings
{{
    public static class ServerConfig
    {{
        public const string BaseUrl = ""{baseUrl}"";
    }}
}}
";
            File.WriteAllText(OutputPath, content);
            AssetDatabase.ImportAsset("Assets/Scripts/Network/ServerConfig.cs",
                ImportAssetOptions.ForceUpdate);
            Debug.Log($"[ServerConfigSync] 서버 주소 갱신 완료: {baseUrl}");
        }
    }
}
