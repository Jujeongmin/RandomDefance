#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 서명된 릴리스 AAB를 빌드합니다.
/// 키스토어 비밀번호는 저장소나 대화에 남지 않도록 환경변수에서만 읽습니다:
///   RD_KEYSTORE_PASS  = 키스토어 비밀번호
///   RD_KEYALIAS_PASS  = 키 별칭 비밀번호 (없으면 키스토어 비밀번호와 동일하게 사용)
/// 출력: 프로젝트 루트의 Builds/randomdefense-v{versionCode}.aab
/// </summary>
public static class ReleaseBuilder
{
    /// <summary>
    /// 프로세스 → 사용자 → 시스템 순으로 환경변수를 찾습니다.
    /// Unity Hub가 트레이에 상주하면 등록 이전의 환경을 물려주므로, 프로세스 환경에
    /// 변수가 없어도 레지스트리에 등록된 사용자 변수까지 봐야 재시작 없이 동작합니다.
    /// </summary>
    static string ReadSecret(string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
    }

    [MenuItem("Tools/Random Defense/Build Release AAB")]
    public static void BuildAab()
    {
        string keystorePass = ReadSecret("RD_KEYSTORE_PASS");
        if (string.IsNullOrEmpty(keystorePass))
            throw new InvalidOperationException("환경변수 RD_KEYSTORE_PASS가 비어 있습니다. 키스토어 비밀번호를 환경변수로 넘겨주세요.");
        string aliasPass = ReadSecret("RD_KEYALIAS_PASS");
        if (string.IsNullOrEmpty(aliasPass)) aliasPass = keystorePass;

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystorePass = keystorePass;
        PlayerSettings.Android.keyaliasPass = aliasPass;

        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false; // 정식 빌드 — 실제 광고 ID 사용

        Directory.CreateDirectory("Builds");
        string output = $"Builds/randomdefense-v{PlayerSettings.Android.bundleVersionCode}.aab";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/MainScene.unity", "Assets/Scenes/GameScene.unity" },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}건");

        Debug.Log($"[ReleaseBuilder] 빌드 성공 — {output} ({report.summary.totalSize / (1024 * 1024)}MB)");
    }
}
#endif
