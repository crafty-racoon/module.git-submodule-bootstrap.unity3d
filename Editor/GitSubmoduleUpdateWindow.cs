using UnityEditor;
using UnityEngine;

namespace CraftyRacoon.GitSubmoduleBootstrap.Editor
{
    internal sealed class GitSubmoduleUpdateWindow : EditorWindow
    {
        private static readonly Vector2 ProgressWindowSize = new Vector2(420f, 150f);
        private static readonly Vector2 BlockedWindowSize = new Vector2(620f, 360f);
        private int missingCount;
        private int safeForwardCount;
        private double openedAt;
        private string blockedMessage;
        private Vector2 scrollPosition;

        internal static GitSubmoduleUpdateWindow OpenProgress(int missingCount, int safeForwardCount)
        {
            GitSubmoduleUpdateWindow window = CreateWindow("Git Submodules", ProgressWindowSize);
            window.missingCount = missingCount;
            window.safeForwardCount = safeForwardCount;
            window.ShowUtility();
            window.Focus();
            return window;
        }

        internal static void OpenBlocked(string message)
        {
            GitSubmoduleUpdateWindow window = CreateWindow("Git Submodule Update Blocked", BlockedWindowSize);
            window.blockedMessage = message;
            window.ShowUtility();
            window.Focus();
        }

        private static GitSubmoduleUpdateWindow CreateWindow(string title, Vector2 size)
        {
            GitSubmoduleUpdateWindow window = CreateInstance<GitSubmoduleUpdateWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = size;
            window.maxSize = size;
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            float x = mainWindow.x + (mainWindow.width - size.x) * 0.5f;
            float y = mainWindow.y + (mainWindow.height - size.y) * 0.5f;
            window.position = new Rect(x, y, size.x, size.y);
            return window;
        }

        private void OnEnable()
        {
            openedAt = EditorApplication.timeSinceStartup;
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(blockedMessage))
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(blockedMessage))
            {
                DrawBlockedState();
                return;
            }

            DrawProgressState();
        }

        private void DrawProgressState()
        {
            GUILayout.Space(14f);
            EditorGUILayout.LabelField("正在安全地同步 Git submodules…", EditorStyles.boldLabel);
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("未初始化：" + missingCount + "　安全前進：" + safeForwardCount);
            GUILayout.Space(10f);
            Rect progressRect = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));
            float progress = (float)((EditorApplication.timeSinceStartup - openedAt) % 1.0);
            EditorGUI.ProgressBar(progressRect, progress, "處理中，請稍候…");
            GUILayout.Space(8f);
            EditorGUILayout.HelpBox("完成後此視窗會自動關閉。", MessageType.Info);
        }

        private void DrawBlockedState()
        {
            GUILayout.Space(10f);
            EditorGUILayout.HelpBox("未執行任何更新。請先處理下列 Git 狀態。", MessageType.Warning);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.SelectableLabel(blockedMessage, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            GUILayout.Space(6f);
            if (GUILayout.Button("Close"))
            {
                Close();
            }
        }
    }
}
