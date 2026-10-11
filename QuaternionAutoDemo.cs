using System.Collections;
using UnityEngine;

namespace QuaternionDemo
{
    /// <summary>
    /// 自动播放约 20 秒的教学演示，既用于课堂展示，也用于确定性视频录制。
    /// </summary>
    [RequireComponent(typeof(QuaternionRotationDemo))]
    public sealed class QuaternionAutoDemo : MonoBehaviour
    {
        private QuaternionRotationDemo demo;
        private Coroutine sequenceCoroutine;

        private void Awake()
        {
            demo = GetComponent<QuaternionRotationDemo>();
        }

        private void Start()
        {
            PlayFullSequence();
        }

        public void PlayFullSequence()
        {
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
            }
            sequenceCoroutine = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            demo.ResetRotation();
            demo.SetChapter("1 / 6   Identity quaternion");
            yield return new WaitForSeconds(1.5f);

            demo.SetChapter("2 / 6   Rotate 90 degrees around X");
            demo.PresetX90();
            yield return WaitForAnimationAndPause();

            demo.SetChapter("3 / 6   Rotate 90 degrees around Y");
            demo.PresetY90();
            yield return WaitForAnimationAndPause();

            demo.SetChapter("4 / 6   Rotate around an arbitrary axis");
            demo.PresetArbitraryAxis();
            yield return WaitForAnimationAndPause();

            demo.SetChapter("5 / 6   Non-commutativity: X then Y");
            demo.ShowRotateXThenY();
            yield return WaitForAnimationAndPause();

            demo.SetChapter("6 / 6   Non-commutativity: Y then X");
            demo.ShowRotateYThenX();
            yield return WaitForAnimationAndPause();

            demo.SetChapter("Demo complete - compare the last two orientations");
            yield return new WaitForSeconds(2.0f);
            sequenceCoroutine = null;
        }

        private IEnumerator WaitForAnimationAndPause()
        {
            yield return null;
            while (demo.IsAnimating)
            {
                yield return null;
            }
            yield return new WaitForSeconds(0.9f);
        }
    }
}
