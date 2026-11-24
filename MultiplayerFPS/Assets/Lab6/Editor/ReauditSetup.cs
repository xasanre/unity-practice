using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Lab6.Editor
{
    public static class ReauditSetup
    {
        const string ReportKey = "Lab6.PlayFixtureReport";

        // Run an existing opt-in runtime fixture in the Editor without building a Player.
        public static void PlayFixture()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-review-play-report");
            if (!UnityEngine.Application.isBatchMode || i < 0 || i + 1 >= args.Length ||
                !Path.IsPathRooted(args[i + 1]) || File.Exists(args[i + 1]))
                throw new InvalidOperationException("Use a batch Editor and a fresh absolute fixture report path.");
            SessionState.SetString(ReportKey, args[i + 1]);
            SessionState.SetFloat(ReportKey + "Deadline", (float)EditorApplication.timeSinceStartup + 320);
            EditorSceneManager.OpenScene(Assignment3Setup.Scenes[0]);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void WatchFixture()
        {
            if (UnityEngine.Application.isBatchMode && Array.IndexOf(Environment.GetCommandLineArgs(), "-review-play-report") >= 0)
                EditorApplication.update += CheckFixture;
        }

        static void CheckFixture()
        {
            var path = SessionState.GetString(ReportKey, "");
            if (path == "") return;
            if (!File.Exists(path) && EditorApplication.timeSinceStartup < SessionState.GetFloat(ReportKey + "Deadline", 0)) return;
            bool passed = File.Exists(path) && File.ReadAllText(path).StartsWith("PASS", StringComparison.Ordinal);
            SessionState.EraseString(ReportKey);
            if (!passed) UnityEngine.Debug.LogError("Play fixture failed or timed out: " + path);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static void ApplyAndBuildTests()
        {
            ReauditLab6Assets.ApplyReauditAssets();
            Lab7Setup.ApplyReauditAudio();
            Assignment3Setup.ApplyReauditAssets();
            Assignment3Setup.SelfCheck();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory("Builds/ReauditTest");
            var report = BuildPipeline.BuildPlayer(Assignment3Setup.Scenes,
                "Builds/ReauditTest/ReauditTest.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            File.WriteAllText("Reaudit.build-report.txt", report.summary.result + "; errors="
                + report.summary.totalErrors + "; warnings=" + report.summary.totalWarnings);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Reaudit development build failed");
        }
    }
}
