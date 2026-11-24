using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace Lab6.Editor
{
    public static class Assignment3Packages
    {
        static AddRequest request;
        static double deadline;
        public static void Install()
        {
            request = Client.Add("com.unity.ai.navigation@2.0.16");
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Poll;
        }
        static void Poll()
        {
            if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Poll;
            bool success = request.IsCompleted && request.Status == StatusCode.Success;
            File.WriteAllText("Assignment3.package-check.txt", success ? "PASS: " + request.Result.name + "@" + request.Result.version : "FAIL: " + (request.Error?.message ?? "Package resolution timed out"));
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
