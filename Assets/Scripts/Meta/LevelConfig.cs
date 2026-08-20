using System;
using System.IO;
using UnityEngine;

namespace FlyingChick
{
    // "레벨업 range 값은 엑셀로 관리하고 유니티와 server에 json으로 import"
    // 요청 -- 진짜 소스는 ~/src/FlyingChick-Server/design/LevelRanges.xlsx,
    // scripts/export_level_ranges.py가 그 엑셀을 읽어서 서버(app/data/
    // level_ranges.json)와 여기(Assets/StreamingAssets/level_ranges.json)
    // 양쪽에 동일한 JSON을 씀 -- 이 파일은 그 결과물을 읽기만 하는 쪽.
    //
    // 값을 바꾸고 싶으면 엑셀을 고치고 export 스크립트를 다시 돌릴 것 --
    // Assets/StreamingAssets/level_ranges.json을 손으로 고치지 말 것(서버가
    // 갖고 있는 사본과 어긋나면, 서버가 계산하는 진짜 레벨과 클라이언트
    // 로컬 표시가 서로 달라짐).
    //
    // Application.streamingAssetsPath는 대부분의 플랫폼(에디터/데스크톱/
    // iOS)에서 File.ReadAllText로 바로 읽히지만, Android(APK 안에 압축돼
    // 있음)와 WebGL은 UnityWebRequest로 읽어야 함 -- 이 프로젝트가 아직
    // 그 플랫폼들을 안 다뤄서(에디터/데스크톱 중심) 지금은 단순한
    // File.ReadAllText만 구현, 나중에 필요해지면 갈아끼울 것.
    public static class LevelConfig
    {
        [Serializable]
        private class LevelRangeEntry
        {
            public int level;
            public float distance;
        }

        [Serializable]
        private class LevelRangeTable
        {
            public LevelRangeEntry[] levels;
        }

        private static LevelRangeEntry[] cachedLevels;

        // 못 찾거나 파싱 실패하면 레벨 1짜리 테이블 하나로 안전하게
        // 폴백함(레벨 시스템이 완전히 죽는 것보단 나음) -- 다른 온라인
        // 기능들과 같은 "실패해도 오프라인처럼 계속 동작" 원칙.
        private static LevelRangeEntry[] Levels
        {
            get
            {
                if (cachedLevels != null) return cachedLevels;

                try
                {
                    string path = Path.Combine(Application.streamingAssetsPath, "level_ranges.json");
                    string json = File.ReadAllText(path);
                    var table = JsonUtility.FromJson<LevelRangeTable>(json);
                    if (table?.levels != null && table.levels.Length > 0)
                    {
                        cachedLevels = table.levels;
                        return cachedLevels;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"LevelConfig: level_ranges.json 로드 실패, 레벨 1로 폴백 ({e.Message})");
                }

                cachedLevels = new[] { new LevelRangeEntry { level = 1, distance = 0f } };
                return cachedLevels;
            }
        }

        // 누적 이동 거리에 해당하는 레벨. 서버(app/level_config.py)와 같은
        // "distance 이상인 가장 높은 level" 규칙 -- 두 파일이 완전히 같은
        // level_ranges.json에서 나온다는 전제.
        public static int LevelForDistance(float totalDistance)
        {
            var levels = Levels;
            int result = levels[0].level;
            foreach (var entry in levels)
            {
                if (totalDistance >= entry.distance) result = entry.level;
                else break;
            }
            return result;
        }
    }
}
