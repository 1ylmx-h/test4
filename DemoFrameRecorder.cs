using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace QuaternionDemo
{
    /// <summary>
    /// 仅在命令行包含 --capture-demo 时启用。逐帧输出 PNG，供 FFmpeg 编码。
    /// </summary>
    public sealed class DemoFrameRecorder : MonoBehaviour
    {
        private const int DefaultFrameRate = 30;
        private const int DefaultFrameCount = 660;

        private string outputDirectory;
        private int frameRate;
        private int frameCount;
        private bool captureEnabled;

        private void Awake()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            captureEnabled = HasArgument(arguments, "--capture-demo");
            if (!captureEnabled)
            {
                enabled = false;
                return;
            }

            outputDirectory = ReadArgument(arguments, "--capture-dir", Path.Combine(Application.dataPath, "..", "CaptureFrames"));
            frameRate = ReadIntegerArgument(arguments, "--capture-fps", DefaultFrameRate);
            frameCount = ReadIntegerArgument(arguments, "--capture-frames", DefaultFrameCount);

            Directory.CreateDirectory(outputDirectory);
            Screen.SetResolution(1280, 720, false);
            Time.captureFramerate = frameRate;
            Application.runInBackground = true;
        }

        private void Start()
        {
            if (captureEnabled)
            {
                StartCoroutine(CaptureFrames());
            }
        }

        private IEnumerator CaptureFrames()
        {
            Camera captureCamera = Camera.main;
            if (captureCamera == null)
            {
                Debug.LogError("[DemoFrameRecorder] Main camera was not found.");
                Application.Quit(3);
                yield break;
            }

            RenderTexture captureTarget = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
            {
                name = "Quaternion Demo Capture Target",
                antiAliasing = 1
            };
            captureTarget.Create();
            Texture2D frameTexture = new Texture2D(1280, 720, TextureFormat.RGB24, false);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();

                RenderTexture previousTarget = captureCamera.targetTexture;
                RenderTexture previousActive = RenderTexture.active;
                captureCamera.targetTexture = captureTarget;
                captureCamera.Render();
                RenderTexture.active = captureTarget;
                frameTexture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0, false);
                frameTexture.Apply(false, false);

                string filePath = Path.Combine(outputDirectory, $"frame_{frameIndex:D5}.png");
                File.WriteAllBytes(filePath, frameTexture.EncodeToPNG());

                captureCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }

            Time.captureFramerate = 0;
            Destroy(frameTexture);
            captureTarget.Release();
            Destroy(captureTarget);

            int writtenFrames = Directory.GetFiles(outputDirectory, "frame_*.png").Length;
            File.WriteAllText(
                Path.Combine(outputDirectory, "capture_complete.txt"),
                $"frames={writtenFrames.ToString(CultureInfo.InvariantCulture)}\n" +
                $"fps={frameRate.ToString(CultureInfo.InvariantCulture)}\n");

            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(writtenFrames == frameCount ? 0 : 2);
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            foreach (string argument in arguments)
            {
                if (string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ReadArgument(string[] arguments, string key, string fallback)
        {
            for (int index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], key, StringComparison.OrdinalIgnoreCase)
                    && index + 1 < arguments.Length)
                {
                    return arguments[index + 1];
                }

                string prefix = key + "=";
                if (arguments[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index].Substring(prefix.Length);
                }
            }
            return fallback;
        }

        private static int ReadIntegerArgument(string[] arguments, string key, int fallback)
        {
            string raw = ReadArgument(arguments, key, fallback.ToString(CultureInfo.InvariantCulture));
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? Mathf.Max(1, value)
                : fallback;
        }
    }
}
