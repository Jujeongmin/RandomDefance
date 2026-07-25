using System;
using UnityEngine;

public enum DailyMissionType
{
    Summon,     // 하루 동안 소환한 횟수
    ReachWave,  // 한 판에서 도달한 최고 웨이브
    KillBoss    // 하루 동안 처치한 보스 수
}

/// <summary>
/// 일일 미션 3종과 7일 순환 출석 보상.
/// 날짜는 기기의 로컬 날짜(yyyyMMdd)를 씁니다. 기기 시계를 과거로 돌리면
/// 갱신을 전부 건너뛰므로 보상을 반복 수령할 수 없습니다.
/// </summary>
public class DailyMissionManager : MonoBehaviour
{
    public const int MissionCount = 3;
    public const int AttendanceCycle = 7;

    static readonly DailyMissionType[] MissionTypes =
    {
        DailyMissionType.Summon, DailyMissionType.ReachWave, DailyMissionType.KillBoss
    };

    PlayerProgressManager m_progress;
    GameBalanceData m_balance;

    /// <summary>진행도·수령 상태가 바뀔 때마다 발생합니다. UI 갱신용.</summary>
    public event Action Changed;

    // 소환은 길게 누르면 0.12초마다 발생하므로 진행도를 메모리에 모았다가 Flush에서 한 번에 저장합니다.
    bool m_dirty;

    public void Initialize(PlayerProgressManager progress, GameBalanceData balance)
    {
        m_progress = progress;
        m_balance = balance;
        RefreshDay();
    }

    static int ToKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;
    static int TodayKey => ToKey(DateTime.Now.Date);
    static int YesterdayKey => ToKey(DateTime.Now.Date.AddDays(-1));

    /// <summary>기기 시계가 마지막 관측일보다 과거면 true. 이 경우 갱신·수령을 모두 막습니다.</summary>
    bool ClockRolledBack => m_progress != null && TodayKey < m_progress.Data.lastSeenDateKey;

    /// <summary>날짜가 바뀌었으면 미션 진행도를 초기화합니다. 앱 시작·복귀·패널 열 때 호출합니다.</summary>
    public void RefreshDay()
    {
        if (m_progress == null || ClockRolledBack) return;

        PlayerProgressData data = m_progress.Data;
        int today = TodayKey;
        bool changed = false;

        if (data.missionDateKey != today)
        {
            data.missionDateKey = today;
            Array.Clear(data.missionProgress, 0, data.missionProgress.Length);
            data.missionClaimedMask = 0;
            changed = true;
        }

        if (data.lastSeenDateKey != today)
        {
            data.lastSeenDateKey = today;
            changed = true;
        }

        if (!changed) return;
        m_progress.Save();
        m_dirty = false;
        Changed?.Invoke();
    }

    // ── 미션 정의 ──

    public static DailyMissionType TypeAt(int index) => MissionTypes[Mathf.Clamp(index, 0, MissionCount - 1)];

    public int GetGoal(int index)
    {
        if (m_balance == null) return 1;
        return TypeAt(index) switch
        {
            DailyMissionType.Summon => m_balance.MissionSummonGoal,
            DailyMissionType.ReachWave => m_balance.MissionWaveGoal,
            _ => m_balance.MissionBossGoal
        };
    }

    public int GetReward(int index) => m_balance != null ? m_balance.GetMissionReward(index) : 0;

    public int GetProgress(int index)
    {
        if (m_progress == null || index < 0 || index >= MissionCount) return 0;
        return Mathf.Min(m_progress.Data.missionProgress[index], GetGoal(index));
    }

    public bool IsComplete(int index) => GetProgress(index) >= GetGoal(index);
    public bool IsClaimed(int index) => m_progress != null && (m_progress.Data.missionClaimedMask & (1 << index)) != 0;
    public bool CanClaim(int index) => IsComplete(index) && !IsClaimed(index);

    public string GetTitle(int index)
    {
        int goal = GetGoal(index);
        return TypeAt(index) switch
        {
            DailyMissionType.Summon => GameLanguage.Choose($"유닛 {goal}회 소환", $"Summon {goal} units"),
            DailyMissionType.ReachWave => GameLanguage.Choose($"웨이브 {goal} 도달", $"Reach wave {goal}"),
            _ => GameLanguage.Choose($"보스 {goal}회 처치", $"Defeat {goal} boss")
        };
    }

    /// <summary>미션 보상을 수령합니다. 지급된 크리스탈을 반환하며, 수령할 수 없으면 0입니다.</summary>
    public int ClaimMission(int index)
    {
        if (m_progress == null || ClockRolledBack || index < 0 || index >= MissionCount) return 0;
        if (!CanClaim(index)) return 0;

        m_progress.Data.missionClaimedMask |= 1 << index;
        int reward = GetReward(index);
        m_progress.AddCrystals(reward); // 저장까지 처리
        m_dirty = false;
        Changed?.Invoke();
        return reward;
    }

    // ── 출석 ──

    /// <summary>오늘 수령할 출석 일차(1~7).</summary>
    public int PendingAttendanceDay
    {
        get
        {
            if (m_progress == null) return 1;
            PlayerProgressData data = m_progress.Data;
            // 어제 받았으면 연속, 아니면 처음부터. 7일을 채웠으면 다시 1일차로 순환합니다.
            int next = data.attendanceDateKey == YesterdayKey ? data.attendanceStreak + 1 : 1;
            return next > AttendanceCycle ? 1 : next;
        }
    }

    /// <summary>이미 수령한 출석 일차(1~7). 아직 한 번도 받지 않았으면 0.</summary>
    public int ClaimedAttendanceDay => m_progress != null ? m_progress.Data.attendanceStreak : 0;

    public bool CanClaimAttendance => m_progress != null && !ClockRolledBack && m_progress.Data.attendanceDateKey != TodayKey;

    public int GetAttendanceReward(int day) => m_balance != null ? m_balance.GetAttendanceReward(day) : 0;

    /// <summary>출석 보상을 수령합니다. 지급된 크리스탈을 반환하며, 오늘 이미 받았으면 0입니다.</summary>
    public int ClaimAttendance()
    {
        if (!CanClaimAttendance) return 0;

        PlayerProgressData data = m_progress.Data;
        int day = PendingAttendanceDay;
        data.attendanceStreak = day;
        data.attendanceDateKey = TodayKey;

        int reward = GetAttendanceReward(day);
        m_progress.AddCrystals(reward); // 저장까지 처리
        m_dirty = false;
        Changed?.Invoke();
        return reward;
    }

    /// <summary>수령 대기 중인 보상이 하나라도 있는지 (메인화면 배지용).</summary>
    public bool HasClaimable
    {
        get
        {
            if (CanClaimAttendance) return true;
            for (int i = 0; i < MissionCount; i++)
                if (CanClaim(i)) return true;
            return false;
        }
    }

    // ── 진행도 알림 (게임씬에서 호출) ──

    public void NotifySummon(int amount = 1) => AddProgress(0, amount);

    public void NotifyWaveReached(int wave) => RaiseProgressTo(1, wave);

    public void NotifyBossDefeated() => AddProgress(2, 1);

    void AddProgress(int index, int amount)
    {
        if (m_progress == null || ClockRolledBack || amount <= 0 || IsComplete(index)) return;
        m_progress.Data.missionProgress[index] += amount;
        m_dirty = true;
        Changed?.Invoke();
    }

    void RaiseProgressTo(int index, int value)
    {
        if (m_progress == null || ClockRolledBack) return;
        if (value <= m_progress.Data.missionProgress[index]) return;
        m_progress.Data.missionProgress[index] = value;
        m_dirty = true;
        Changed?.Invoke();
    }

    /// <summary>모아 둔 진행도를 디스크에 씁니다. 판이 끝나거나 앱이 백그라운드로 갈 때 호출합니다.</summary>
    public void Flush()
    {
        if (!m_dirty || m_progress == null) return;
        m_dirty = false;
        m_progress.Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) Flush();
        else RefreshDay(); // 앱을 켜 둔 채 자정을 넘긴 경우
    }

    void OnApplicationQuit() => Flush();
}
